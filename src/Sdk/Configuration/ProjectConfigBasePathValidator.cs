using Microsoft.Extensions.Options;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Rejects a <see cref="ProjectConfig.BasePath"/> that is an absolute URL
/// (starts with <c>http://</c> or <c>https://</c>) with an explicit, actionable error.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProjectConfig.BasePath"/> is a subdirectory prefix (e.g. <c>/photos/</c>),
/// not a host. Previously an absolute URL here was silently mangled into a nonsensical
/// path (e.g. <c>/https://example.com/</c>); this validator turns that mistake into a
/// hard failure that points at <see cref="ProjectConfig.BaseUrl"/> for the host instead.
/// </para>
/// <para>
/// Mirrors <see cref="ProjectConfigLanguageValidator"/>: a small, targeted
/// <see cref="IValidateOptions{TOptions}"/> that runs lazily on first access.
/// </para>
/// </remarks>
internal sealed class ProjectConfigBasePathValidator : IValidateOptions<ProjectConfig>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProjectConfig options)
    {
        var basePath = options.BasePath;

        if (basePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            basePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"'basePath' must be a site-relative subdirectory (e.g. \"/photos/\"), not an absolute URL (\"{basePath}\"). " +
                "Set the host in project.baseUrl instead (e.g. \"https://example.com\") and use basePath only for the path prefix.");
        }

        return ValidateOptionsResult.Success;
    }
}
