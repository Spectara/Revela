using System.Globalization;

using Spectre.Console;

namespace Spectara.Revela.Sdk.Output;

/// <summary>
/// Plain-text progress for consoles that cannot render a live progress bar
/// (CI, pipes, redirected output).
/// </summary>
/// <remarks>
/// <para>
/// Use it as the fallback when <see cref="Hosting.IConsoleCapabilities.CanRenderLive"/> is
/// <c>false</c>, so long-running work is never silent. It writes one dim line per 10 % step,
/// e.g. <c>Compressed 40/100 file(s)</c>.
/// </para>
/// <para>
/// Reports may arrive from parallel workers. Lines are serialized and only ever move
/// forward: a report for an earlier step is ignored.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var stats = consoleCapabilities.CanRenderLive
///     ? await CompressWithProgressBarAsync(cancellationToken)
///     : await service.CompressAsync(new PlainProgressReporter("Compressed"), cancellationToken);
/// </code>
/// </example>
/// <param name="verb">Past-tense verb that starts each line (e.g. <c>Downloaded</c>).</param>
/// <param name="console">Console to write to; defaults to <see cref="AnsiConsole.Console"/>.</param>
public sealed class PlainProgressReporter(string verb, IAnsiConsole? console = null)
    : IProgress<(int Current, int Total)>, IProgress<(int Current, int Total, string Item)>
{
    private readonly Lock gate = new();
    private readonly string escapedVerb = Markup.Escape(verb);
    private int lastStep = -1;

    /// <inheritdoc />
    public void Report((int Current, int Total) value) => Report(value.Current, value.Total);

    /// <inheritdoc />
    /// <remarks>The item name is not printed; plain output only reports counts.</remarks>
    public void Report((int Current, int Total, string Item) value) => Report(value.Current, value.Total);

    private void Report(int current, int total)
    {
        if (total <= 0)
        {
            return;
        }

        var step = current * 10 / total;
        lock (gate)
        {
            if (step <= lastStep)
            {
                return;
            }

            lastStep = step;
            (console ?? AnsiConsole.Console).MarkupLine(string.Create(
                CultureInfo.InvariantCulture,
                $"[dim]{escapedVerb} {current}/{total} file(s)[/]"));
        }
    }
}
