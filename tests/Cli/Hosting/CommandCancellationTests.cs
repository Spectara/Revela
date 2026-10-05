using System.CommandLine;
using System.Runtime.InteropServices;

using Spectara.Revela.Cli.Hosting;

namespace Spectara.Revela.Tests.Cli.Hosting;

[TestClass]
[TestCategory("Unit")]
public sealed class CommandCancellationTests
{
    [TestMethod]
    public async Task InvokeAsync_CtrlCWhileCommandStopsGracefully_ReturnsCancelledExitCode()
    {
        using var cancellation = CommandCancellation.CreateUnregistered();
        var command = CreateCommand(async token =>
        {
            cancellation.Request(PosixSignal.SIGINT);
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                // A long-running command such as serve stops cleanly and reports success.
            }

            return ExitCodes.Success;
        });

        var exitCode = await cancellation.InvokeAsync(command.Parse([]));

        Assert.AreEqual(130, exitCode);
    }

    [TestMethod]
    public async Task InvokeAsync_CtrlCWhileCommandThrowsOperationCanceled_ReturnsCancelledExitCode()
    {
        using var cancellation = CommandCancellation.CreateUnregistered();
        var command = CreateCommand(async token =>
        {
            cancellation.Request(PosixSignal.SIGINT);
            await Task.Delay(Timeout.Infinite, token);
            return ExitCodes.Success;
        });

        var exitCode = await cancellation.InvokeAsync(command.Parse([]));

        Assert.AreEqual(130, exitCode);
    }

    [TestMethod]
    public async Task InvokeAsync_Sigterm_ReturnsTerminatedExitCode()
    {
        using var cancellation = CommandCancellation.CreateUnregistered();
        var command = CreateCommand(async token =>
        {
            cancellation.Request(PosixSignal.SIGTERM);
            await Task.Delay(Timeout.Infinite, token);
            return ExitCodes.Success;
        });

        var exitCode = await cancellation.InvokeAsync(command.Parse([]));

        Assert.AreEqual(143, exitCode);
    }

    [TestMethod]
    public async Task InvokeAsync_NoSignal_ReturnsCommandExitCode()
    {
        using var cancellation = CommandCancellation.CreateUnregistered();
        var command = CreateCommand(_ => Task.FromResult(ExitCodes.ConfigurationProblem));

        var exitCode = await cancellation.InvokeAsync(command.Parse([]));

        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public async Task InvokeAsync_CommandTimesOutWithoutSignal_PropagatesException()
    {
        using var cancellation = CommandCancellation.CreateUnregistered();
        var command = CreateCommand(_ => throw new TaskCanceledException("HTTP request timed out"));

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => cancellation.InvokeAsync(command.Parse([])));
    }

    private static RootCommand CreateCommand(Func<CancellationToken, Task<int>> action)
    {
        var command = new RootCommand();
        command.SetAction((_, token) => action(token));
        return command;
    }
}
