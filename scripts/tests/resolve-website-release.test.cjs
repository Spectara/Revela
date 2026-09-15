const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const workflow = fs.readFileSync(path.join(__dirname, '../../.github/workflows/deploy-website.yml'), 'utf8')
  .replace(/\r\n/g, '\n');
const scriptBlock = workflow.match(/^          script: \|\n((?: {12}[^\n]*\n|\n)+)/m);
assert.ok(scriptBlock, 'Expected the inline release resolver');
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const resolveRelease = new AsyncFunction('require', 'github', 'context', 'core', 'process', 'getOctokit',
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

test('Cosign 3 emits and publishes verification bundles for artifacts and checksums', () => {
  const signing = releaseWorkflow.split('      - name: Sign artifacts (keyless)\n')[1]
    .split('      - name: Upload signatures and checksums\n')[0];
  assert.ok(releaseWorkflow.includes('uses: sigstore/cosign-installer@v4'));
  assert.ok(releaseWorkflow.includes('cosign-release: v3.1.3'));
  assert.equal((signing.match(/cosign sign-blob/g) ?? []).length, 2);
  assert.ok(signing.includes('--bundle "${outBase}.sigstore.json"'));
  assert.ok(signing.includes('--bundle ../signatures/SHA256SUMS.sigstore.json'));
  assert.ok(!signing.includes('--output-signature'));
  assert.ok(!signing.includes('--output-certificate'));
  assert.ok(!signing.includes('--tlog-upload=false'));
  assert.ok(!signing.includes('--use-signing-config=false'));
  const releaseJob = releaseWorkflow.split('\n  release:\n')[1].split('\n  publish-nuget:\n')[0];
  assert.ok(releaseJob.includes('artifacts/**/*.sigstore.json'));
  assert.ok(releaseJob.includes('artifacts/**/SHA256SUMS'));
  const publishingJob = releaseWorkflow.split('\n  publish-nuget:\n')[1];
  assert.ok(publishingJob.includes('\n    if: false\n'));
});

test('CI and Release default to read-only contents while publishing jobs retain scoped rights', () => {
  const ci = fs.readFileSync(path.join(__dirname, '../../.github/workflows/ci.yml'), 'utf8')
    .replace(/\r\n/g, '\n');
  for (const content of [ci, releaseWorkflow]) {
    assert.match(content, /^permissions:\n  contents: read\n\n/m);
  }
  for (const jobName of ['packages', 'build']) {
    const job = releaseWorkflow.split(`\n  ${jobName}:\n`)[1].split(/^  [\w-]+:\n/m)[0];
    assert.match(job, /    permissions:\n      contents: read\n      id-token: write\n      attestations: write\n/);
  }
  const signingJob = releaseWorkflow.split('\n  sign:\n')[1].split('\n  release:\n')[0];
  assert.match(signingJob, /    permissions:\n      contents: read\n      id-token: write\n/);
  const releaseJob = releaseWorkflow.split('\n  release:\n')[1].split('\n  publish-nuget:\n')[0];
  assert.match(releaseJob, /    permissions:\n      contents: write\n/);
});

test('release validation passes untrusted values only through env to a constant pwsh command', () => {
  const validationJob = releaseWorkflow.split('\n  validate:\n')[1].split('\n  packages:\n')[0];
  assert.equal((validationJob.match(/- name: Validate release version\n/g) ?? []).length, 1);
  assert.match(validationJob, /        id: version\n        shell: pwsh\n        env:\n/);
  for (const [name, expression] of [
    ['RELEASE_EVENT', 'github.event_name'],
    ['RELEASE_INPUT_VERSION', 'inputs.version'],
    ['RELEASE_REF_NAME', 'github.ref_name'],
    ['RELEASE_REF_TYPE', 'github.ref_type']
  ]) {
    assert.ok(validationJob.includes(`          ${name}: \${{ ${expression} }}\n`));
  }
  assert.deepEqual(validationJob.match(/^        run:.*$/gm), ['        run: ./scripts/validate-release-version.ps1']);
  assert.ok(validationJob.includes('version: ${{ steps.version.outputs.version }}'));
  assert.ok(!validationJob.includes('sort -V'));
  assert.ok(!validationJob.includes('name: Extract version'));
  assert.ok(!validationJob.includes('name: Validate version format'));
});

const repositoryRoot = path.resolve(__dirname, '../..');
const validatorPath = path.join(repositoryRoot, 'scripts/validate-release-version.ps1');
const versionFixtureRoot = path.join(repositoryRoot, 'artifacts/release-version-tests');
const outputSentinel = 'sentinel=unchanged\n';
const validatorHarness = `
$ErrorActionPreference = 'Stop'
$global:ReleaseGitCalls = 0
function git {
    $global:ReleaseGitCalls++
    if ($args.Count -ne 3 -or $args[0] -cne 'tag' -or $args[1] -cne '--list' -or $args[2] -cne 'v*') {
        throw 'Unexpected git invocation in release validator.'
    }
    $expectedRoot = Split-Path -Parent (Split-Path -Parent $env:TEST_RELEASE_VALIDATOR)
    if ((Get-Location).Path -cne $expectedRoot) {
        throw 'Release tag enumeration must run in the actual repository.'
    }
    $global:LASTEXITCODE = [int]$env:TEST_RELEASE_GIT_EXIT_CODE
    ConvertFrom-Json -InputObject $env:TEST_RELEASE_TAGS
}
try {
    & $env:TEST_RELEASE_VALIDATOR
    $status = 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $status = 1
}
finally {
    [IO.File]::WriteAllText($env:TEST_RELEASE_GIT_CALLS, [string]$global:ReleaseGitCalls)
}
exit $status
`;

function executeVersion({ env = {}, tags = [], gitExitCode = 0 } = {}) {
  fs.mkdirSync(versionFixtureRoot, { recursive: true });
  const directory = fs.mkdtempSync(path.join(versionFixtureRoot, 'case-'));
  const outputPath = path.join(directory, 'github-output.txt');
  const gitCallsPath = path.join(directory, 'git-calls.txt');
  fs.writeFileSync(outputPath, outputSentinel, 'utf8');
  const childEnv = { ...process.env,
    RELEASE_EVENT: 'workflow_dispatch', RELEASE_INPUT_VERSION: '1.2.3',
    RELEASE_REF_NAME: 'main', RELEASE_REF_TYPE: 'branch', GITHUB_OUTPUT: outputPath,
    TEST_RELEASE_VALIDATOR: validatorPath, TEST_RELEASE_TAGS: JSON.stringify(tags),
    TEST_RELEASE_GIT_EXIT_CODE: String(gitExitCode), TEST_RELEASE_GIT_CALLS: gitCallsPath,
    ...env };
  for (const [name, value] of Object.entries(childEnv)) {
    if (value === null) delete childEnv[name];
  }
  const result = spawnSync('pwsh', ['-NoLogo', '-NoProfile', '-NonInteractive', '-Command', validatorHarness], {
    cwd: directory, env: childEnv, encoding: 'utf8', shell: false
  });
  assert.ifError(result.error);
  assert.equal(result.signal, null);
  assert.equal(result.stdout, '');
  return { ...result, output: fs.readFileSync(outputPath),
    gitCalls: Number(fs.readFileSync(gitCallsPath, 'utf8')) };
}

function assertVersionSuccess(result, version, gitCalls = 0) {
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stderr, '');
  assert.deepEqual(result.output, Buffer.from(`${outputSentinel}version=${version}\n`, 'utf8'));
  assert.equal(result.gitCalls, gitCalls);
}

function assertVersionFailure(result, expectedError, gitCalls = 0) {
  assert.equal(result.status, 1);
  assert.match(result.stderr, expectedError);
  assert.deepEqual(result.output, Buffer.from(outputSentinel, 'utf8'));
  assert.equal(result.gitCalls, gitCalls);
}

for (const version of ['1.2.3', '1.2.3-beta', '1.2.3-beta.1', '0.0.0', '1.0.0-0', '1.0.0-01a.0']) {
  test(`manual release accepts ${version} without consulting newer or equal tags`, () => {
    assertVersionSuccess(executeVersion({ env: { RELEASE_INPUT_VERSION: version },
      tags: [`v${version}`, 'v99.0.0'], gitExitCode: 97 }), version);
  });
}

const invalidReleaseVersions = [
  ['missing input', null], ['empty input', ''], ['leading whitespace', ' 1.2.3'],
  ['trailing whitespace', '1.2.3 '], ['trailing newline', '1.2.3\n'],
  ['CRLF output injection', '1.2.3\r\nversion=9.0.0'], ['tab', '1.2.3\t'],
  ['command substitution', '$(printf PROBE)'], ['embedded command substitution', '1.2.3-$(printf PROBE)'],
  ['backticks', '`printf PROBE`'], ['double quote', '1.2.3"'], ["single quote", "1.2.3'"],
  ['command separator', '1.2.3;printf PROBE'], ['slash', '../1.2.3'], ['v prefix', 'v1.2.3'],
  ['leading-zero major', '01.2.3'], ['leading-zero minor', '1.02.3'], ['leading-zero patch', '1.2.03'],
  ['leading-zero numeric label', '1.2.3-01'], ['leading-zero numeric label with suffix', '1.2.3-01.1'],
  ['leading-zero prerelease number', '1.2.3-beta.01'], ['hyphenated label', '1.2.3-beta-1'],
  ['multiple textual identifiers', '1.2.3-beta.rc'], ['multiple numeric suffixes', '1.2.3-beta.1.2'],
  ['build metadata', '1.2.3+build.1'], ['non-ASCII digits', '\u0661.2.3'],
  ['parser numeric overflow', '2147483648.0.0']
];

for (const [name, version] of invalidReleaseVersions) {
  test(`release validator rejects ${name} without output or input disclosure`, () => {
    const result = executeVersion({ env: { RELEASE_INPUT_VERSION: version,
      RELEASE_REF_NAME: 'v9.0.0', RELEASE_REF_TYPE: 'tag' } });
    assertVersionFailure(result, /Invalid or missing release version/);
    if (version) assert.ok(!result.stderr.includes(version));
  });
}

for (const [name, env, expectedError] of [
  ['unknown event', { RELEASE_EVENT: 'pull_request' }, /Unsupported or missing release event/],
  ['missing event', { RELEASE_EVENT: null }, /Unsupported or missing release event/],
  ['push branch', { RELEASE_EVENT: 'push', RELEASE_REF_NAME: 'v1.2.3' }, /v-prefixed tag ref/],
  ['push without ref type', { RELEASE_EVENT: 'push', RELEASE_REF_TYPE: null }, /v-prefixed tag ref/],
  ['push without ref', { RELEASE_EVENT: 'push', RELEASE_REF_TYPE: 'tag', RELEASE_REF_NAME: null }, /v-prefixed tag ref/],
  ['push without v prefix', { RELEASE_EVENT: 'push', RELEASE_REF_TYPE: 'tag', RELEASE_REF_NAME: '1.2.3' }, /v-prefixed tag ref/],
  ['push with uppercase prefix', { RELEASE_EVENT: 'push', RELEASE_REF_TYPE: 'tag', RELEASE_REF_NAME: 'V1.2.3' }, /v-prefixed tag ref/],
  ['push cannot use manual fallback', { RELEASE_EVENT: 'push', RELEASE_REF_TYPE: 'tag', RELEASE_REF_NAME: 'v' }, /Invalid or missing release version/],
  ['missing output path', { GITHUB_OUTPUT: null }, /GITHUB_OUTPUT is required/]
]) {
  test(`release validator fails closed for ${name}`, () => {
    assertVersionFailure(executeVersion({ env }), expectedError);
  });
}

function executePush(version, options = {}) {
  return executeVersion({ ...options, env: { RELEASE_EVENT: 'push', RELEASE_REF_TYPE: 'tag',
    RELEASE_REF_NAME: `v${version}`, RELEASE_INPUT_VERSION: '$(printf PROBE)' } });
}

for (const [name, tags] of [['no historical tags', []], ['blank tag listing', ['']], ['only the exact current tag', ['v1.0.0']]]) {
  test(`first push release accepts ${name} and ignores manual input`, () => {
    assertVersionSuccess(executePush('1.0.0', { tags }), '1.0.0', 1);
  });
}

for (const [current, previous, newer] of [
  ['1.0.0', '1.0.0-rc.1', true], ['1.0.0-rc.1', '1.0.0', false],
  ['1.0.0-beta.10', '1.0.0-beta.2', true], ['1.0.0-beta.2', '1.0.0-beta.10', false],
  ['1.0.0-rc.1', '1.0.0-beta.99', true], ['2.0.0', '1.99.99', true],
  ['1.0.0-a', '1.0.0-9', true], ['1.0.0-9', '1.0.0-a', false],
  ['1.0.0-10', '1.0.0-2', true], ['1.0.0-2', '1.0.0-10', false],
  ['1.0.0-alpha', '1.0.0-ALPHA', true], ['1.0.0-ALPHA', '1.0.0-alpha', false],
  ['1.0.0-beta.1', '1.0.0-beta', true], ['1.0.0-beta', '1.0.0-beta.1', false]
]) {
  test(`push ${current} after ${previous} follows SemVer (${newer ? 'accept' : 'reject'})`, () => {
    const result = executePush(current, { tags: [`v${current}`, `v${previous}`] });
    if (newer) assertVersionSuccess(result, current, 1);
    else assertVersionFailure(result, /must be strictly newer/, 1);
  });
}

test('push compares against the maximum historical version, not the final listed tag', () => {
  assertVersionFailure(executePush('2.0.0', { tags: ['v3.0.0', 'v2.0.0', 'v1.0.0'] }), /must be strictly newer/, 1);
});

for (const tag of ['v01.0.0', 'v1.0.0-beta.01', 'v1.0.0-01', 'v1.0.0-beta-1', 'v1.0.0+build', 'v1x0x0', 'v', 'v$(printf PROBE)']) {
  test(`push fails closed for invalid historical tag ${tag}`, () => {
    const result = executePush('1.0.0', { tags: ['v1.0.0', tag] });
    assertVersionFailure(result, /Invalid historical v-prefixed release tag/, 1);
    assert.equal(result.stderr.replace(/\r\n/g, '\n'),
      'Invalid historical v-prefixed release tag. Expected canonical X.Y.Z[-alphanumeric[.digits]].\n');
  });
}

test('failed git enumeration does not publish a partial listing or append an output', () => {
  assertVersionFailure(executePush('1.0.0', { tags: ['v0.1.0'], gitExitCode: 128 }), /Unable to enumerate release tags/, 1);
});