using System.Text.RegularExpressions;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Asserts the heading outline that accessibility audits (Lighthouse/axe <c>heading-order</c>) check:
/// headings start at <c>h1</c> and never skip a level when descending.
/// </summary>
/// <remarks>
/// The closed site menu popover counts too: it precedes <c>&lt;main&gt;</c>, and crawlers and
/// outline tools read it even though accessibility audits skip hidden content.
/// </remarks>
internal static partial class HeadingOrderAssert
{
    public static void Sequential(string page, string html)
    {
        var previous = 0;
        foreach (Match heading in HeadingPattern().Matches(html))
        {
            var level = heading.Groups["level"].Value[0] - '0';
            Assert.IsLessThanOrEqualTo(
                previous + 1,
                level,
                $"{page}: <h{level}> follows <h{previous}> and skips a heading level.");
            previous = level;
        }
    }

    [GeneratedRegex(@"<h(?<level>[1-6])[\s>]")]
    private static partial Regex HeadingPattern();
}
