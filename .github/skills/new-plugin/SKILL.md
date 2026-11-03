---
name: new-plugin
description: "Scaffold a new Revela plugin — project, csproj, plugin class, command, config, tests"
argument-hint: "[plugin-name]"
user-invocable: true
disable-model-invocation: true
---

# New Plugin Scaffold

Create a new external plugin under `src/Plugins/${pluginPath}/`. `${pluginName}`
is the leaf project/type name (for example `Dropbox`); `${pluginPath}` includes
any existing group (for example `Source/Dropbox`). These are collected values,
not VS Code input variables.

Use **Revela Dev**. If that agent is already active, work here; otherwise
explicitly delegate to it. Skills do not switch agents automatically. If the
agent is unavailable, report the limitation rather than silently changing personas.

## Inputs (ask the user if not supplied)

1. **Plugin name and group** — PascalCase leaf name (e.g. `Notify`, `Backup`, or `Dropbox` in the existing `Source` group)
2. **Description** — one-sentence summary
3. **Parent command** — `null` (root), `"source"`, `"generate"`, `"theme"`, or other
4. **Requires project** — `true` (needs `project.json`) or `false`
5. **Needs HttpClient** — yes/no (typed client)
6. **Needs configuration** — yes/no (`[RevelaConfig]` class)

## Steps

Use the **Revela Dev** agent for actual implementation. Follow the conventions in [`.github/instructions/plugins.instructions.md`](../../instructions/plugins.instructions.md).

1. **Create csproj** at `src/Plugins/${pluginPath}/${pluginName}.csproj`
   - Reference [Sdk.csproj](../../../src/Sdk/Sdk.csproj) with `Private=false` and `ExcludeAssets=runtime`, as [Serve.csproj](../../../src/Plugins/Serve/Serve.csproj) does.
   - Set `IsPackable=true`, `PackageType=RevelaPlugin` and `IsAotCompatible=true`; include the README and any declared icon in the package.
   - Inherit assembly/package names, namespaces and output paths from the applicable `Directory.Build.props` files. Do not duplicate the `Spectara.Revela` prefix or hardcode an output path. Use an existing group; a new group needs matching source/test build conventions.
2. **Add to solution** — `dotnet sln Spectara.Revela.slnx add <new-project-path>`
3. **Plugin class** — `${pluginName}Plugin.cs` implementing `IPlugin` with `PackageMetadata`
4. **(Optional) Config class** — `Configuration/${pluginName}Config.cs` with `[RevelaConfig("plugins:<key>")]` and a matching `public const string Section = "plugins:<key>";` (`<key>` is the camelCase plugin key, e.g. `myFeature`). Follow the scoped binding/validation rules; persist only through `IPluginSettingsWriter<TConfig>`.
5. **Command class** — `Commands/${VerbName}Command.cs` (partial, with primary constructor DI)
6. **Wire the embedded host** — add a project reference in [Cli.Embedded.csproj](../../../src/Cli.Embedded/Cli.Embedded.csproj) and register the plugin in [EmbeddedPackageSource](../../../src/Cli.Embedded/EmbeddedPackageSource.cs) so F5 debugging includes it.
7. **Test project** — `tests/Plugins/${pluginPath}/${pluginName}.csproj`; add it to the solution.
   - Reference the plugin and [Shared.csproj](../../../tests/Shared/Shared.csproj); mirror the existing MSTest/NSubstitute setup.
   - Test meaningful command behavior and error/non-interactive paths. For configuration, cover an intentional default and the settings writer contract. Do not assert hardcoded metadata strings merely to satisfy a test count.
8. **README** — short `src/Plugins/${pluginPath}/README.md` with usage example
9. **Sample (if user-facing)** — add invocation to `samples/showcase` if appropriate

## Validation Gate
After scaffolding:
- `dotnet build` — must succeed
- `dotnet test --project <new-test-project-path>` — must pass
- `dotnet format --verify-no-changes` — must be clean
- Verify embedded command discovery with `dotnet run --no-build --project src\Cli.Embedded -- <command-path> --help`.
- For a distributable plugin, use the existing [packaging checks](../../../docs/development.md#building-releases); a managed build alone does not establish package loading or AOT compatibility.

## Hand-off

Tell the user:
- Where the new files live
- How to invoke the command (`revela ${parent} ${verb} --help`)
- Which behavior is implemented, what remains scaffold-only, and which checks actually ran
