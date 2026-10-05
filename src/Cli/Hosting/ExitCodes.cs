namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Process exit codes of the <c>revela</c> CLI.
/// </summary>
/// <remarks>
/// Public contract, documented in the CLI reference (Exit Codes). Commands and plugins
/// return 0 or 1; the host maps configuration problems and termination signals.
/// </remarks>
internal static class ExitCodes
{
    /// <summary>The command completed successfully.</summary>
    public const int Success = 0;

    /// <summary>The command failed (invalid input, missing files, unexpected error, ...).</summary>
    public const int Error = 1;

    /// <summary>A configuration file or value is invalid, or two plugins claim the same settings.</summary>
    public const int ConfigurationProblem = 2;

    /// <summary>Cancelled with Ctrl+C (128 + SIGINT).</summary>
    public const int Cancelled = 130;

    /// <summary>Stopped by SIGTERM, e.g. <c>docker stop</c> (128 + SIGTERM).</summary>
    public const int Terminated = 143;
}
