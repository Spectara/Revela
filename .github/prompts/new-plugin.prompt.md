---
mode: agent
description: "Scaffold a new Revela plugin — project, csproj, plugin class, command, config, tests"
---

# New Plugin Scaffold

Create a new external plugin for the Revela project under `src/Plugins/${input:pluginName:e.g. MyFeature}/`.

## Inputs (ask the user if not supplied)

1. **Plugin name** — PascalCase (e.g. `Notify`, `Backup`, `Source/Dropbox`)
2. **Description** — one-sentence summary
3. **Parent command** — `null` (root), `"source"`, `"generate"`, `"theme"`, or other
4. **Requires project** — `true` (needs `project.json`) or `false`
5. **Needs HttpClient** — yes/no (typed client)
6. **Needs configuration** — yes/no (`[RevelaConfig]` class)

## Steps

Use the **Revela Dev** agent for actual implementation. Follow the conventions in [`.github/instructions/plugins.instructions.md`](../instructions/plugins.instructions.md).

1. **Create csproj** at `src/Plugins/${pluginName}/Spectara.Revela.Plugins.${pluginName}.csproj`
   - Reference `src/Sdk/Spectara.Revela.Sdk.csproj`
   - Output to `artifacts/bin/${pluginName}/` (centrally configured via `Directory.Build.props`)
2. **Add to solution** — `dotnet sln add src/Plugins/${pluginName}/...`
3. **Plugin class** — `${PluginName}Plugin.cs` implementing `IPlugin` with `PackageMetadata`
4. **(Optional) Config class** — `Configuration/${PluginName}Config.cs` with `[RevelaConfig("Spectara.Revela.Plugins.${PluginName}")]`
5. **Command class** — `Commands/${VerbName}Command.cs` (partial, with primary constructor DI)
6. **Register in `EmbeddedPackageSource`** — `src/Cli.Embedded/EmbeddedPackageSource.cs` so F5 debugging includes the new plugin
7. **Test project** — `tests/Plugins/${pluginName}/Spectara.Revela.Tests.Plugins.${pluginName}.csproj`
   - Reference plugin + `tests/Shared`
   - At least one test for `${PluginName}Plugin.Metadata` and one for the command's happy path
8. **README** — short `src/Plugins/${pluginName}/README.md` with usage example
9. **Sample (if user-facing)** — add invocation to `samples/showcase` if appropriate

## Validation Gate
After scaffolding:
- `dotnet build` — must succeed
- `dotnet test tests/Plugins/${pluginName}` — must pass
- `dotnet format --verify-no-changes` — must be clean

## Hand-off

Tell the user:
- Where the new files live
- How to invoke the command (`revela ${parent} ${verb} --help`)
- What's needed next (real implementation in `${VerbName}Command.ExecuteAsync`)
