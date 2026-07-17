using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Rejects a stray <c>language</c> setting left in the <c>project</c> section of
/// <c>project.json</c> with an explicit, actionable error.
/// </summary>
/// <remarks>
/// <para>
/// <c>language</c> moved from <c>project.json</c> to <c>site.json</c> (see #75).
/// Silently ignoring the old key would leave users puzzled when their configured
/// language has no effect, so this validator turns it into a hard failure that
/// points at <c>site.json</c>.
/// </para>
/// <para>
/// It inspects the raw configuration keys directly (trim/AOT-safe — no reflection)
/// because <see cref="ProjectConfig"/> no longer has a <c>Language</c> property to bind.
/// Detection is by key <em>presence</em>, so even an empty or null value
/// (<c>"language": ""</c>) is rejected.
/// </para>
/// </remarks>
/// <param name="configuration">The application configuration.</param>
internal sealed class ProjectConfigLanguageValidator(IConfiguration configuration)
    : IValidateOptions<ProjectConfig>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProjectConfig options)
    {
        var hasLanguage = configuration
            .GetSection(ProjectConfig.Section)
            .GetChildren()
            .Any(child => child.Key.Equals("language", StringComparison.OrdinalIgnoreCase));

        if (hasLanguage)
        {
            return ValidateOptionsResult.Fail(
                "'language' has moved from project.json to site.json. " +
                "Remove \"language\" from the \"project\" section of project.json and " +
                "set it in site.json instead (e.g. { \"language\": \"en\" }).");
        }

        return ValidateOptionsResult.Success;
    }
}
