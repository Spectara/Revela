using System.Runtime.CompilerServices;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

[assembly: InternalsVisibleTo("revela")]
[assembly: InternalsVisibleTo("Spectara.Revela.Tests.Commands")]
[assembly: InternalsVisibleTo("Spectara.Revela.Tests.Integration")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

// Emit config-key constants for the SDK config POCOs this assembly writes.
[assembly: RevelaConfigKeys(typeof(ProjectConfig))]
