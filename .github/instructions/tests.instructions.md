---
applyTo: "tests/**/*.cs"
description: "Test conventions for Revela — MSTest v4 + NSubstitute + custom fixtures"
---

# Test Conventions — Revela

## Stack
- **MSTest v4** (Microsoft.Testing.Platform) — NOT MSTest v3, NOT xUnit, NOT NUnit.
- **NSubstitute** for mocking — NOT Moq (security concerns), NOT FluentAssertions (removed).
- **Microsoft Code Coverage** via `--coverage` flag — settings in `coverage.config`. NOT Coverlet.

## Test Class Layout
```csharp
namespace Spectara.Revela.Tests.Core;

[TestClass]
[TestCategory("Unit")]   // or "Integration", "E2E"
public sealed class MyServiceTests
{
    [TestMethod]
    public async Task DoSomethingAsync_WithEmptyInput_ReturnsEmpty()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MyService>>();
        var sut = new MyService(logger);

        // Act
        var result = await sut.DoSomethingAsync([]);

        // Assert (MSTest v4 built-in only)
        Assert.IsEmpty(result);
    }
}
```

## Naming
- **Test class:** `<ClassUnderTest>Tests`.
- **Test method:** `MethodName_Condition_ExpectedResult` (e.g. `Parse_NullInput_ThrowsArgumentNullException`).

## Assertions — MSTest v4 (no FluentAssertions!)
```csharp
Assert.AreEqual(expected, actual);
Assert.IsTrue(condition);
Assert.IsNull(value);  Assert.IsNotNull(value);
Assert.IsEmpty(collection);  Assert.IsNotEmpty(collection);
Assert.HasCount(3, collection);
Assert.Contains(item, collection);
Assert.ThrowsExactly<ArgumentNullException>(() => sut.Do(null!));
await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await sut.DoAsync());
```

## Three Test Layers
| Layer | What | Where |
|-------|------|-------|
| **Unit** | Pure logic, no I/O | `tests/Core`, `tests/Commands`, `tests/Plugins/*` |
| **Integration** | Real filesystem via `TestProject` + `RevelaTestHost` | `tests/Integration` |
| **E2E** | Full pipeline (scan → render → images) with `TestImageGenerator` | `tests/Integration` |

## Test Fixtures (`tests/Shared/Fixtures/`)
- `TestProject.Create(p => p.AddGallery(...))` — fluent builder for temp project dirs.
- `RevelaTestHost` — builds real DI container with `IOptions<T>` from `project.json`.
- `TestImageGenerator.CreateJpeg(path, exif: ...)` — real JPEGs with EXIF via NetVips.
- `GalleryBuilder.AddImage()` — 4-byte JPEG stub (fast scan tests).
- `GalleryBuilder.AddRealImage()` — real JPEG (E2E).

Use `InternalsVisibleTo` (already configured) to test internal classes — don't make them public.

## What NOT to Test
- ❌ **C# language** — `Assert.AreEqual(42, foo.Value)` after `foo.Value = 42`.
- ❌ **Framework** — that `IOptions<T>` resolves (Microsoft's job).
- ❌ **Hardcoded strings** — `Assert.AreEqual("Serve", metadata.Name)` is a tautology.
- ❌ **Duplicate tests** — keep the one with the better assertion.
- ❌ **"Doesn't throw" tests** — every test needs a meaningful assertion.

## What IS Worth Testing
- ✅ **Default-value tests** — they prevent accidental config changes.
- ✅ **Computed properties** — `TotalFiles = New + Modified` is our logic.
- ✅ **Edge cases** — empty/null/large/unicode inputs.
- ✅ **Error paths** — exceptions, validation failures.

## Cross-Platform (Linux CI is case-sensitive!)
- `UrlBuilder.ToSlug()` lowercases all names → output paths are always lowercase.
- File path assertions: use lowercase slugs (`"landscapes"`, not `"Landscapes"`).

## Coverage Discipline
- `coverage.config` excludes framework wiring — only OUR decisions are measured.
- When adding new `ServiceCollectionExtensions`, command `Create()` methods, or plugin lifecycle methods → check if `coverage.config` Functions/Sources excludes need updating.

## Run
```pwsh
dotnet test                                    # all
dotnet test tests/Core                         # one project
dotnet test --filter "TestCategory=Unit"       # by category
dotnet test --coverage                         # with coverage
```
