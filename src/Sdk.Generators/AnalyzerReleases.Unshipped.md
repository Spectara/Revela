; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
REVELA001 | Revela.Configuration | Error | PluginConfigSectionGenerator: plugin/theme configuration section must be plugins:<key>
REVELA002 | Revela.Configuration | Error | PluginConfigSectionGenerator: [RevelaConfig] argument and Section constant differ
REVELA003 | Revela.Configuration | Error | PluginConfigBindingAnalyzer: plugin/theme binds configuration outside its own [RevelaConfig] section
