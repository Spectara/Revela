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
dotnet test --coverage --coverage-settings coverage.config
```

Microsoft Code Coverage runs through Microsoft.Testing.Platform. Coverage filters
are configured in `coverage.config`. Add `--coverage-output-format cobertura` for
Cobertura output, and `--report-trx --results-directory ./TestResults` for TRX reports.

---

## Building Releases

For theme work, also run the [Lumina browser acceptance](../scripts/browser/README.md)
against fresh Showcase output. It verifies actual viewport/JS settings and the
page/lightbox/none journeys; .NET tests alone do not prove browser usability.

### Create Standalone Executable

```bash
# Windows
dotnet publish src/Cli -c Release -r win-x64 --self-contained

# Linux
dotnet publish src/Cli -c Release -r linux-x64 --self-contained

# macOS
dotnet publish src/Cli -c Release -r osx-arm64 --self-contained
```

### Test Release Pipeline

```bash
./scripts/test-release.ps1
```

This runs the full release pipeline: build, pack, plugin install, generate, compress, clean, idempotency check, and dotnet tool install.

### CI Release Dry Runs

The [Release workflow](../.github/workflows/release.yml) accepts a version through
`workflow_dispatch` to validate packages and platform builds without creating a
GitHub Release or automatically deploying the website. The
[Deploy Website workflow](../.github/workflows/deploy-website.yml) runs automatically
only after a successful tag-triggered Release workflow. Its own manual dispatch
remains available for intentional content deployments using the latest release.

Manual Release runs still attest and sign artifacts. They are not externally
side-effect-free checks and must be started intentionally.

These workflow changes must be present on the default branch before a Release
dry run is started. A tag push publishes a release; it is not a dry run.

### Build Release Bundle

```bash
./scripts/build-release.ps1
```

Builds local release bundles that mirror the GitHub release pipeline output.
Three variants: `Standalone` (self-contained, all plugins linked statically),
`Full` (CLI + all plugins as `.nupkg` for `revela plugin install`), and `Core`
(CLI only). Use `-Variant Standalone|Full|Core|All` to pick.

---

## Getting Help

- **GitHub Issues:** [Report bugs or request features](https://github.com/spectara/revela/issues)
- **Discussions:** [Ask questions](https://github.com/spectara/revela/discussions)
- **Architecture:** [Architecture Overview](architecture.md)
