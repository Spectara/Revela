using Microsoft.Extensions.Configuration;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Warns about settings below <c>plugins</c> that no installed plugin uses
/// (typically a typo such as <c>plugins:serv</c>).
/// </summary>
public sealed partial class UnclaimedPluginConfigReporter(
    IConfiguration configuration,
    PluginConfigOwnership ownership,
    ILogger<UnclaimedPluginConfigReporter> logger)
{
    /// <summary>
    /// Logs one warning per unclaimed key.
    /// </summary>
    public void Report()
    {
        foreach (var unclaimed in ownership.FindUnclaimedKeys(configuration))
        {
            if (unclaimed.Suggestion is null)
            {
                LogUnclaimedKey(logger, unclaimed.Key);
            }
            else
            {
                LogUnclaimedKeyWithSuggestion(logger, unclaimed.Key, unclaimed.Suggestion);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configuration 'plugins:{Key}' is not used by any installed plugin.")]
    private static partial void LogUnclaimedKey(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configuration 'plugins:{Key}' is not used by any installed plugin. Did you mean '{Suggestion}'?")]
    private static partial void LogUnclaimedKeyWithSuggestion(ILogger logger, string key, string suggestion);
}
