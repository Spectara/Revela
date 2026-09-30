namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Thrown when two loaded packages claim the same key below <c>plugins</c>.
/// </summary>
public sealed class PluginConfigConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PluginConfigConflictException"/> class.</summary>
    public PluginConfigConflictException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PluginConfigConflictException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public PluginConfigConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PluginConfigConflictException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginConfigConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PluginConfigConflictException"/> class.</summary>
    /// <param name="key">The contested key.</param>
    /// <param name="firstPackageId">The package that claimed the key first.</param>
    /// <param name="secondPackageId">The package that claimed the key again.</param>
    public PluginConfigConflictException(string key, string firstPackageId, string secondPackageId)
        : base($"Packages '{firstPackageId}' and '{secondPackageId}' both claim the configuration key 'plugins:{key}'. Uninstall one of them.")
    {
        Key = key;
        FirstPackageId = firstPackageId;
        SecondPackageId = secondPackageId;
    }

    /// <summary>The contested key.</summary>
    public string Key { get; } = string.Empty;

    /// <summary>The package that claimed the key first.</summary>
    public string FirstPackageId { get; } = string.Empty;

    /// <summary>The package that claimed the key again.</summary>
    public string SecondPackageId { get; } = string.Empty;
}
