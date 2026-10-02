using System.Text;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;

using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk.Json;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// The one way Revela writes a JSON configuration file (<c>revela.json</c>,
/// <c>project.json</c>, <c>site.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// A write is all-or-nothing and immediately visible to the running process:
/// </para>
/// <list type="number">
/// <item>The candidate is parsed by the JSON configuration reader first, so a document the
/// host could not load at the next start (e.g. <c>"a:b"</c> colliding with <c>{"a":{"b"}}</c>)
/// throws the reader's exception and the file stays untouched.</item>
/// <item>It is written to a hidden temporary file next to the target and moved over it with
/// <see cref="AtomicFileReplace"/>; readers never see a partial file.</item>
/// <item><see cref="IConfigurationRoot.Reload"/> runs afterwards. Configuration sources don't
/// watch files, and the reload token also re-binds every <c>IOptionsMonitor&lt;T&gt;</c>.</item>
/// </list>
/// <para>
/// On Unix a new file is created owner-only (<c>0600</c>); an existing file keeps its mode
/// unless the caller passes an explicit one.
/// </para>
/// </remarks>
public sealed partial class ConfigFileWriter(IConfiguration configuration, ILogger<ConfigFileWriter> logger)
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>
    /// Validates, atomically writes and reloads a JSON configuration file.
    /// </summary>
    /// <param name="path">Absolute path of the file; missing directories are created.</param>
    /// <param name="content">The complete document to write.</param>
    /// <param name="unixFileMode">
    /// Unix permissions to apply. <see langword="null"/> keeps the mode of an existing file.
    /// </param>
    /// <param name="cancellationToken">Cancels before the file is replaced.</param>
    /// <exception cref="FormatException">The configuration reader rejects the document (e.g. duplicate keys).</exception>
    public async Task WriteAsync(
        string path,
        JsonNode content,
        UnixFileMode? unixFileMode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = Encoding.UTF8.GetBytes(content.ToJsonString(RevelaJsonOptions.Write));
        Validate(bytes);

        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("Configuration path has no parent directory.", nameof(path));
        _ = Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };

        var targetMode = unixFileMode;
        if (!OperatingSystem.IsWindows())
        {
            streamOptions.UnixCreateMode = OwnerOnly;
            if (targetMode is null && File.Exists(path))
            {
                targetMode = File.GetUnixFileMode(path);
            }
        }

        try
        {
            await using (var stream = new FileStream(temporaryPath, streamOptions))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows() && targetMode is { } mode)
            {
                File.SetUnixFileMode(temporaryPath, mode);
            }

            await AtomicFileReplace.ReplaceAsync(temporaryPath, path, cancellationToken);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }

        LogWritten(path);
        (configuration as IConfigurationRoot)?.Reload();
    }

    private static void Validate(byte[] json)
    {
        using var stream = new MemoryStream(json, writable: false);
        using var validation = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
    }

    private void DeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogTemporaryFileCleanupFailed(temporaryPath, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Wrote configuration file '{Path}'")]
    private partial void LogWritten(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove temporary configuration file '{TemporaryPath}' ({Error})")]
    private partial void LogTemporaryFileCleanupFailed(string temporaryPath, string error);
}
