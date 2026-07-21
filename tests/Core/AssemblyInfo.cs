using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

// Enable parallel test execution at assembly level
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

// Emit config-key constants exercised by ConfigKeysGeneratorTests.
[assembly: RevelaConfigKeys(typeof(ProjectConfig))]
[assembly: RevelaConfigKeys(typeof(GenerateConfig))]
[assembly: RevelaConfigKeys(typeof(PathsConfig))]
[assembly: RevelaConfigKeys(typeof(ThemeConfig))]


