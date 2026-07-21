using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

// Emit config-key constants for the theme config POCO ThemeService writes.
[assembly: RevelaConfigKeys(typeof(ThemeConfig))]
