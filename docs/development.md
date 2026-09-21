# Development Guide

Guide for contributors who want to build, test, and develop Revela.

> For initial setup (prerequisites, clone, build, IDE configuration), see [Setup & Build Instructions](setup.md).
> For project layout and namespace conventions, see [Project Structure](project-structure.md).
> For plugin development, see [Plugin Development](plugin-development.md).

---

## Testing

### Framework

- **MSTest v4** - Test framework
- **NSubstitute** - Mocking
- **Built-in assertions** - No FluentAssertions

### Test Patterns

```csharp
// Collection assertions (MSTest v4)
Assert.IsEmpty(list);
Assert.HasCount(3, list);
Assert.Contains("item", collection);

// String assertions
Assert.Contains("substring", text);
Assert.DoesNotContain("bad", text);
```

### Code Coverage

```bash
dotnet test --solution Spectara.Revela.slnx --coverage --coverage-settings coverage.config
```

Microsoft Code Coverage runs through Microsoft.Testing.Platform. Coverage filters
are configured in `coverage.config`. Add `--coverage-output-format cobertura` for
Cobertura output, and `--report-trx --results-directory ./TestResults` for TRX reports.

---

## Building Releases

For theme work, use the retained [Lumina browser requirements](../scripts/browser/README.md)
against fresh isolated output. The former standalone browser script has been
removed; no replacement automated browser suite is currently available. Perform
manual or available isolated browser checks and report gaps explicitly. .NET
tests alone do not prove browser usability. A future Playwright integration is
a separate decision, not part of the pipeline-toolchain cleanup.

The [CI/CD guide](../.github/workflows/README.md#release-stages) describes release verification.
Run the following PowerShell examples from the Revela repository root, only when
you own the build/output resources. Do not run Build mode or release publishing
concurrently with another build using the same repository outputs.

### Build Release Bundles

[build-release.ps1](../scripts/build-release.ps1) accepts `-Variant Standalone`,
`Full` or `All` (default). It writes unpacked release-shaped directories under
`artifacts/releases/`, not ZIP/TAR archives. It does **not** accept `Core`;
the Release workflow produces Core archives.

```pwsh
.\scripts\build-release.ps1 -Variant Standalone -Version 0.0.0-test -RuntimeIdentifier win-x64
.\scripts\build-release.ps1 -Variant Full -Version 0.0.0-test -RuntimeIdentifier win-x64
```

Choose your actual OS/architecture instead of `win-x64` when appropriate.
`Standalone` is the Native AOT `Cli.Embedded` host with plugins and themes linked
statically, plus the platform's native libvips companion. It has no plugin
management and needs a native build toolchain (MSVC on Windows, gcc/clang on
Linux, Xcode CLT on macOS). Publishing `src/Cli` as self-contained does not create
this Standalone variant.

`Full` uses the modular CLI with plugins, themes, SDK and CLI tool packages in
`packages/`. `All` builds both variants. `-SkipBuild` reuses the solution build
but still publishes; it is not an artifact-only verification mode. Bundle
creation by itself does not establish that the release tests passed.

### Test a Local Build

Without `-ArtifactPath`, [test-release.ps1](../scripts/test-release.ps1) selects
Build mode, which produces and tests **Full** only:

```pwsh
.\scripts\test-release.ps1 -Version 0.0.0-test
```

It restores/builds the Release solution, runs its full tests, publishes the CLI,
packs existing Release binaries and runs the shared modular suite. This covers
package installation and registration, theme commands, generation, compression,
cleanup, idempotency, installing the exact CLI tool package locally and consuming
the packaged SDK. Lumina comes from its release package, not an injected loose DLL.

`-SkipTests` is Build-only and skips unit tests, not the modular suite. Build mode
is a local opt-in check; CI no longer runs it on every change.
`-IncludeOneDrive` adds a real network download and requires a valid share; it is
optional and unavailable for Standalone. Local package/SDK dependency restores
may also need network access; omitting OneDrive is not a guarantee of an entirely
network-free run.

### Test Existing Release Artifacts

Supplying `-ArtifactPath` selects Artifact mode. It accepts a first-party extracted
release directory, `.zip` or `.tar.gz` strictly inside this Revela checkout.
`-PackageDirectory`, when needed, must also be inside the checkout. Linked inputs
are rejected. Stage downloads there before testing, leaving the original download
and samples untouched. The executable must be at the input directory/archive root.

Replace `<VERSION>` and the example paths below with existing matching inputs.
These Windows x64 examples are alternatives, not a script to run unchanged:

```pwsh
.\scripts\test-release.ps1 -ArtifactPath .\artifacts\downloads\revela-win-x64-full.zip -Variant Full -Version '<VERSION>' -RuntimeIdentifier win-x64
.\scripts\test-release.ps1 -ArtifactPath .\artifacts\downloads\revela-win-x64-core.zip -Variant Core -PackageDirectory .\artifacts\downloads\nupkgs -Version '<VERSION>' -RuntimeIdentifier win-x64
.\scripts\test-release.ps1 -ArtifactPath .\artifacts\downloads\revela-win-x64-standalone.zip -Variant Standalone -Version '<VERSION>' -RuntimeIdentifier win-x64
```

An extracted directory can replace the archive path. On a Linux x64 host, for
example, use the corresponding `revela-linux-x64-full.tar.gz` and
`-RuntimeIdentifier linux-x64`. The test RID must match the current host's OS and
OS architecture; cross-runtime execution is rejected. Omitting the RID detects
the host, not the artifact. The requested version must exactly match the binary
and tested packages; it is not inferred from the archive name. Unix executables
must already have owner execute permission; the test does not repair the archive.

| Variant    | Inputs and checks                                                                                                                                                   |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Full       | Uses its bundled `packages/` only; `-PackageDirectory` is rejected. Runs the modular suite.                                                                         |
| Core       | Requires explicit `-PackageDirectory` with the same release's packages. Runs the modular suite using this external fixture, not a shipped theme/feed.               |
| Standalone | Requires its actual native libvips companion. Tests the Native AOT host, configuration, generation and cleanup without plugin management or SDK/tool package tests. |

Artifact mode skips product restore/build/publish/pack entirely. Full/Core may
still build the isolated SDK consumer and install the existing CLI tool package
as tests; they do not repack it. The SDK consumer uses
[test-sdk-consumer.ps1](../scripts/test-sdk-consumer.ps1) with `-PackageDirectory`
and the exact `-Version` to avoid rebuilding the SDK package.

Core's initial package installation uses an explicit local `--source`. For the
R1 parallel-restore regression only, the suite temporarily copies the supplied
fixture packages into the staged CLI's `packages/` directory so restore selects
those bytes. It removes that temporary feed afterward and verifies restored DLL
SHA256 hashes against the supplied package entries. This proves that fixture's
registration/restore behavior, not that Core includes a usable theme out of the
box or can obtain unpublished packages from NuGet.org.

The suite stages copies and isolates test configuration/caches under a unique
`artifacts/release-test-<timestamp>-<id>/` directory. It records
`verification.log`, `input-hashes.json` and `after-hashes.json`, comparing SHA256
hashes and the file inventory of supplied artifacts/feed and original Showcase
inputs. Test directories are always retained, including after failures; remove
unneeded test directories explicitly. SDK consumer evidence is retained
separately under `artifacts/sdk-consumer-<id>/`.

### Hosted CI and Release Runs

[CI](../.github/workflows/ci.yml) runs Release builds and full-solution tests with
coverage on Ubuntu. The generator builds through its normal project reference.
Format verification sets the environment variable `Configuration=Release` to use
the same output; no separate Debug build is needed.
TRX/Cobertura uploads come from `TestResults`. Packaging and Native AOT execution
are checked in Release, not on every push or pull request. Windows/macOS-specific
unit tests require an explicit local run; release artifact tests do not replace
those full unit suites.

[Release](../.github/workflows/release.yml) runs `validate -> packages -> build ->
sign -> release -> website`. Packages are tested before no-build packing,
then checked through an isolated SDK consumer. The five native RID jobs test
their actual Core, Full and Standalone archives before archive attestation and
signing, without rebuilding the product in the tests. The `release` job creates
a GitHub Release only on a tag push. NuGet packages are included as release
downloads; publishing to NuGet.org is not configured and requires separate setup
and authorization.

Manual Release dispatch takes `version` and does not create a GitHub Release or
automatically deploy the website. It still attests and signs, with external
effects. Any remote workflow run, tag push or deployment requires explicit
permission; none is authorized by this guide.

[Website deployment](../.github/workflows/deploy-website.yml) is called directly
after a successful tag-push Release publication, with that run's commit and tag.
A manual **Deploy Website** dispatch remains available for emergency content
corrections: provide `source-ref` (default `main`) and an explicit existing
`release-tag` including `v`. Both entry paths require a published, non-draft
release and run the same generation/Pages checks. Neither falls back to latest;
manual Release rehearsals do not deploy. Use content compatible with the selected
binary, or a correction based on the published release if `main` has moved ahead.

There is no custom pipeline-contract test suite. Use `actionlint` for workflow
syntax and review permissions and publication conditions directly; the old
synthetic version/deployment cases are no longer automated. No replacement
test framework or Node/npm dependency is introduced.
See the [CI/CD guide](../.github/workflows/README.md) for the coverage trade-off,
stage details and checksum/signing verification. Local checks do not establish
hosted event, signing or Pages integration success. Report actual run results
and verification gaps with the task or release, not as permanent review diaries.

---

## Getting Help

- **GitHub Issues:** [Report bugs or request features](https://github.com/spectara/revela/issues)
- **Discussions:** [Ask questions](https://github.com/spectara/revela/discussions)
- **Architecture:** [Architecture Overview](architecture.md)
