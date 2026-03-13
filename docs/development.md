# Development Guide

Guide for contributors who want to build, test, and develop Revela.

> For initial setup (prerequisites, clone, build, IDE configuration), see [Setup & Build Instructions](setup.md).
> For project layout and namespace conventions, see [Project Structure](project-structure.md).
> For plugin development, see [Plugin Development Guide](plugin-development.md).

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
dotnet test --collect:"XPlat Code Coverage"
```

Coverage filters are configured in `coverage.config`.

---

## Building Releases

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

### Build Standalone Bundle

```bash
./scripts/build-standalone.ps1
```

Creates a self-contained release with all plugins and themes bundled.

---

## Getting Help

- **GitHub Issues:** [Report bugs or request features](https://github.com/spectara/revela/issues)
- **Discussions:** [Ask questions](https://github.com/spectara/revela/discussions)
- **Architecture:** [Architecture Overview](architecture.md)
