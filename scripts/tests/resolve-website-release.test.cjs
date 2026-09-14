const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const workflow = fs.readFileSync(path.join(__dirname, '../../.github/workflows/deploy-website.yml'), 'utf8')
  .replace(/\r\n/g, '\n');
const scriptBlock = workflow.match(/^          script: \|\n((?: {12}[^\n]*\n|\n)+)/m);
assert.ok(scriptBlock, 'Expected the inline release resolver');
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const resolveRelease = new AsyncFunction('require', 'github', 'context', 'core', 'process',
  scriptBlock[1].replace(/^ {12}/gm, ''));

function fixture() {
  const commit = 'a'.repeat(40);
  return {
    identity: { tag: 'v1.2.3-beta.1', version: '1.2.3-beta.1', commit, runId: '123', runAttempt: '2' },
    event: { workflow_run: { id: 123, run_attempt: 2, head_sha: commit, head_branch: 'main',
      event: 'push', conclusion: 'success' } },
    release: { tag_name: 'v1.2.3-beta.1', draft: false, prerelease: true, target_commitish: 'main' },
    tagCommit: { sha: commit }
  };
}

async function execute(input) {
  const outputs = {};
  const calls = [];
  const github = { rest: { repos: {
    async getReleaseByTag(request) {
      calls.push({ method: 'getReleaseByTag', ...request });
      if (input.releaseError) throw input.releaseError;
      return { data: input.release };
    },
    async getCommit(request) {
      calls.push({ method: 'getCommit', ...request });
      if (input.commitError) throw input.commitError;
      return { data: input.tagCommit };
    }
  } } };
  const fakeRequire = (name) => {
    if (name !== 'node:fs') return require(name);
    return {
      readFileSync(file, encoding) {
        assert.equal(file, path.join('runner-temp', 'release-identity', 'release-metadata.json'));
        assert.equal(encoding, 'utf8');
        if (input.missingFile) throw new Error('ENOENT: release-metadata.json');
        return input.rawMetadata ?? JSON.stringify(input.identity);
      }
    };
  };
  let error;
  try {
    await resolveRelease(fakeRequire, github,
      { payload: input.event, repo: { owner: 'Spectara', repo: 'Revela' } },
      { setOutput: (name, value) => { outputs[name] = value; } },
      { env: { RUNNER_TEMP: 'runner-temp' } });
  } catch (caught) {
    error = caught;
  }
  return { outputs, calls, error };
}

test('uses exact prerelease tag and SHA, independent of branch and release target_commitish', async () => {
  const input = fixture();
  const result = await execute(input);
  assert.equal(result.error, undefined);
  assert.deepEqual(result.outputs, { ref: input.identity.commit, tag: input.identity.tag });
  assert.deepEqual(result.calls, [
    { method: 'getReleaseByTag', owner: 'Spectara', repo: 'Revela', tag: input.identity.tag },
    { method: 'getCommit', owner: 'Spectara', repo: 'Revela', ref: `refs/tags/${input.identity.tag}` }
  ]);
});

test('accepts stable versions and numeric run identity values', async () => {
  const input = fixture();
  Object.assign(input.identity, { tag: 'v1.2.3', version: '1.2.3', runId: 123, runAttempt: 2 });
  Object.assign(input.release, { tag_name: 'v1.2.3', prerelease: false });
  const result = await execute(input);
  assert.equal(result.error, undefined);
  assert.deepEqual(result.outputs, { ref: input.identity.commit, tag: 'v1.2.3' });
});

const metadataFailures = [
  ['missing metadata file', (input) => { input.missingFile = true; }, /ENOENT/],
  ['malformed metadata JSON', (input) => { input.rawMetadata = '{'; }, /JSON/],
  ['null metadata', (input) => { input.identity = null; }, /Missing release identity/],
  ['array metadata', (input) => { input.identity = []; }, /Missing release identity/],
  ['missing run id', (input) => { delete input.identity.runId; }, /runId/],
  ['wrong run id', (input) => { input.identity.runId = '124'; }, /runId/],
  ['invalid run id type', (input) => { input.identity.runId = [123]; }, /runId/],
  ['missing attempt', (input) => { delete input.identity.runAttempt; }, /runAttempt/],
  ['wrong attempt', (input) => { input.identity.runAttempt = '1'; }, /runAttempt/],
  ['wrong head SHA', (input) => { input.identity.commit = 'b'.repeat(40); }, /head SHA/],
  ['invalid commit format', (input) => { input.identity.commit = input.event.workflow_run.head_sha = 'main'; }, /head SHA/],
  ['missing version', (input) => { delete input.identity.version; }, /tag\/version/],
  ['invalid version', (input) => { input.identity.version = '../latest'; input.identity.tag = 'v../latest'; }, /tag\/version/],
  ['newline in version', (input) => { input.identity.version += '\n'; input.identity.tag += '\n'; }, /tag\/version/],
  ['mismatched tag/version', (input) => { input.identity.tag = 'v1.2.4'; }, /tag\/version/],
  ['manual Release run', (input) => { input.event.workflow_run.event = 'workflow_dispatch'; }, /successful tag-triggered/],
  ['failed Release run', (input) => { input.event.workflow_run.conclusion = 'failure'; }, /successful tag-triggered/],
  ['missing workflow run', (input) => { delete input.event.workflow_run; }, /successful tag-triggered/]
];

for (const [name, mutate, expectedError] of metadataFailures) {
  test(`fails before API calls for ${name}`, async () => {
    const input = fixture();
    mutate(input);
    const result = await execute(input);
    assert.match(result.error?.message ?? '', expectedError);
    assert.deepEqual(result.outputs, {});
    assert.deepEqual(result.calls, []);
  });
}

const releaseFailures = [
  ['missing release', (input) => { input.release = null; }, /published release/],
  ['draft release', (input) => { input.release.draft = true; }, /published release/],
  ['missing draft state', (input) => { delete input.release.draft; }, /published release/],
  ['wrong release tag', (input) => { input.release.tag_name = 'v1.2.4'; }, /published release/],
  ['release API failure', (input) => { input.releaseError = new Error('Release not found (404)'); }, /404/],
  ['moved tag', (input) => { input.tagCommit.sha = 'b'.repeat(40); }, /triggering commit/],
  ['missing resolved commit', (input) => { input.tagCommit = null; }, /triggering commit/],
  ['tag resolution failure', (input) => { input.commitError = new Error('Tag not found (404)'); }, /404/]
];

for (const [name, mutate, expectedError] of releaseFailures) {
  test(`fails closed for ${name}`, async () => {
    const input = fixture();
    mutate(input);
    const result = await execute(input);
    assert.match(result.error?.message ?? '', expectedError);
    assert.deepEqual(result.outputs, {});
    assert.ok(result.calls.every((call) => ['getReleaseByTag', 'getCommit'].includes(call.method)));
  });
}

function step(name) {
  const marker = `      - name: ${name}\n`;
  const start = workflow.indexOf(marker);
  assert.notEqual(start, -1, `Missing step: ${name}`);
  const end = workflow.indexOf('\n      - name:', start + marker.length);
  return workflow.slice(start, end === -1 ? undefined : end);
}

test('automatic source and binary use resolver outputs with no latest fallback', () => {
  const checkout = step('Checkout triggering release source');
  const download = step('Download triggering Revela release (Standalone)');
  assert.ok(checkout.includes("if: github.event_name == 'workflow_run'"));
  assert.ok(checkout.includes('ref: ${{ steps.resolve-release.outputs.ref }}'));
  assert.ok(download.includes("if: github.event_name == 'workflow_run'"));
  assert.ok(download.includes('tag: ${{ steps.resolve-release.outputs.tag }}'));
  assert.ok(!download.includes('latest:'));
  assert.ok(!scriptBlock[1].includes('getLatestRelease'));
  assert.ok(workflow.indexOf('      - name: Resolve triggering release\n') <
    workflow.indexOf('      - name: Checkout triggering release source\n'));
  assert.ok(step('Replace download placeholders with actual release URLs')
    .includes('VERSION: ${{ steps.resolve-release.outputs.tag || steps.download-release.outputs.tag_name }}'));
});

test('manual checkout keeps the selected ref and downloads latest including prereleases', () => {
  const checkout = step('Checkout');
  const download = step('Download latest Revela release (Standalone)');
  assert.ok(checkout.includes("if: github.event_name == 'workflow_dispatch'"));
  assert.ok(!checkout.includes('ref:'));
  assert.ok(download.includes("if: github.event_name == 'workflow_dispatch'"));
  assert.ok(download.includes('id: download-release'));
  assert.ok(download.includes('latest: true'));
  assert.ok(download.includes('preRelease: true'));
});

test('identity is fetched from the triggering run outside checkout and manual Release runs stay excluded', () => {
  const download = step('Download triggering release identity');
  assert.ok(download.includes("if: github.event_name == 'workflow_run'"));
  assert.ok(download.includes('uses: actions/download-artifact@v8'));
  assert.ok(download.includes('name: release-identity-${{ github.event.workflow_run.run_attempt }}'));
  assert.ok(download.includes('run-id: ${{ github.event.workflow_run.id }}'));
  assert.ok(download.includes('github-token: ${{ github.token }}'));
  assert.ok(download.includes('path: ${{ runner.temp }}/release-identity'));
  assert.ok(workflow.includes('  actions: read\n'));
  assert.ok(workflow.includes("github.event.workflow_run.conclusion == 'success'"));
  assert.ok(workflow.includes("github.event.workflow_run.event == 'push'"));
});

const releaseWorkflow = fs.readFileSync(path.join(__dirname, '../../.github/workflows/release.yml'), 'utf8')
  .replace(/\r\n/g, '\n');

test('producer serializes the exact identity after publishing and isolates run attempts', () => {
  const expression = releaseWorkflow.match(/node -e '([^'\n]+)'/);
  assert.ok(expression, 'Expected release metadata producer');
  const env = { RELEASE_TAG: 'v1.2.3-beta.1', RELEASE_VERSION: '1.2.3-beta.1',
    RELEASE_COMMIT: 'a'.repeat(40), GITHUB_RUN_ID: '123', GITHUB_RUN_ATTEMPT: '2' };
  const files = new Map();
  const fakeFs = { mkdirSync: (directory) => assert.equal(directory, 'release-identity'),
    writeFileSync: (file, contents) => files.set(file, contents) };
  new Function('require', 'process', expression[1])((name) => {
    assert.equal(name, 'node:fs');
    return fakeFs;
  }, { env });
  assert.deepEqual(JSON.parse(files.get('release-identity/release-metadata.json')), fixture().identity);
  assert.ok(releaseWorkflow.indexOf('uses: softprops/action-gh-release@v3') <
    releaseWorkflow.indexOf('- name: Record published release identity'));
  assert.ok(releaseWorkflow.includes('name: release-identity-${{ github.run_attempt }}'));
  const releaseJob = releaseWorkflow.split('\n  release:\n')[1].split('\n  publish-nuget:\n')[0];
  assert.ok(releaseJob.includes("if: github.event_name == 'push'"));
});

test('actual package and archive checks precede attestation and CI runs these contracts', () => {
  for (const [testName, attestation] of [
    ['Verify packaged SDK consumer', 'Attest build provenance'],
    ['Verify final release archives', 'Attest Core build provenance']
  ]) {
    assert.ok(releaseWorkflow.indexOf(`- name: ${testName}`) >= 0);
    assert.ok(releaseWorkflow.indexOf(`- name: ${testName}`) < releaseWorkflow.indexOf(`- name: ${attestation}`));
  }
  assert.ok(releaseWorkflow.includes("@('Core', 'Full', 'Standalone')"));
  assert.ok(releaseWorkflow.includes('ArtifactPath = "./revela-'));
  const ci = fs.readFileSync(path.join(__dirname, '../../.github/workflows/ci.yml'), 'utf8');
  assert.ok(ci.includes('node --test scripts/tests/resolve-website-release.test.cjs'));
  assert.ok(ci.includes('dotnet format Spectara.Revela.slnx --verify-no-changes'));
  assert.ok(ci.includes('./TestResults/**/*.cobertura.xml'));
});