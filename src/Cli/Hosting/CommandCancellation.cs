using System.CommandLine;
using System.Runtime.InteropServices;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Owns Ctrl+C (SIGINT) and SIGTERM while one command runs, for direct runs and for
/// commands started from the interactive menu alike.
/// </summary>
/// <remarks>
/// <para>
/// The first signal cancels the command's <see cref="CancellationToken"/> instead of killing
/// the process, so commands such as <c>serve</c> can shut down cleanly. A second signal is not
/// intercepted and terminates the process, in case a command ignores its token.
/// </para>
/// <para>
/// System.CommandLine's own termination handling is switched off
/// (<see cref="InvocationConfiguration.ProcessTerminationTimeout"/> = <see langword="null"/>),
/// so this is the only handler and the exit code is deterministic: <see cref="ExitCodes.Cancelled"/>
/// after Ctrl+C and <see cref="ExitCodes.Terminated"/> after SIGTERM, whatever the command returned.
/// </para>
/// </remarks>
internal sealed class CommandCancellation : IDisposable
{
    private readonly CancellationTokenSource source;
    private readonly PosixSignalRegistration? interruptRegistration;
    private readonly PosixSignalRegistration? terminateRegistration;
    private int signalExitCode;

    private CommandCancellation(bool listenToSignals, CancellationToken cancellationToken)
    {
        source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (listenToSignals)
        {
            interruptRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnSignal);
            terminateRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);
        }
    }

    /// <summary>
    /// Gets the token passed to the command; cancelled by the first signal or by the outer token.
    /// </summary>
    public CancellationToken Token => source.Token;

    private bool IsSignaled => Volatile.Read(ref signalExitCode) != 0;

    /// <summary>
    /// Starts listening for Ctrl+C and SIGTERM until disposed.
    /// </summary>
    /// <param name="cancellationToken">Outer token that also cancels the command.</param>
    public static CommandCancellation Listen(CancellationToken cancellationToken = default) =>
        new(listenToSignals: true, cancellationToken);

    /// <summary>
    /// Creates an instance that is only signalled through <see cref="Request"/> (tests).
    /// </summary>
    internal static CommandCancellation CreateUnregistered(CancellationToken cancellationToken = default) =>
        new(listenToSignals: false, cancellationToken);

    /// <summary>
    /// Handles a termination signal: records its exit code and cancels the command.
    /// </summary>
    /// <returns><see langword="false"/> when an earlier signal was already handled.</returns>
    public bool Request(PosixSignal signal)
    {
        var exitCode = signal == PosixSignal.SIGTERM ? ExitCodes.Terminated : ExitCodes.Cancelled;
        if (Interlocked.CompareExchange(ref signalExitCode, exitCode, 0) != 0)
        {
            return false;
        }

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The signal raced with the end of the command; nothing left to cancel.
        }

        return true;
    }

    /// <summary>
    /// Invokes the parsed command with <see cref="Token"/>.
    /// </summary>
    /// <returns>
    /// The command's exit code, or the signal's exit code when a signal arrived during the run.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The command was cancelled by something other than a signal (e.g. an HTTP timeout).
    /// </exception>
    public async Task<int> InvokeAsync(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        // No default exception handler: configuration validation failures must reach the
        // callers' catch blocks and be rendered as friendly panels instead of stack traces.
        var configuration = new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
            ProcessTerminationTimeout = null,
        };

        try
        {
            var exitCode = await parseResult.InvokeAsync(configuration, Token);
            return IsSignaled ? signalExitCode : exitCode;
        }
        catch (OperationCanceledException) when (IsSignaled)
        {
            return signalExitCode;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        interruptRegistration?.Dispose();
        terminateRegistration?.Dispose();
        source.Dispose();
    }

    private void OnSignal(PosixSignalContext context) => context.Cancel = Request(context.Signal);
}
