using BenchmarkDotNet.Running;

namespace Spectara.Revela.Benchmarks.ImageProcessing;

public class Program
{
    // Run with: dotnet run -c Release -- --filter *
    // Or specific: dotnet run -c Release -- --filter *ProcessImage*
    // Quick check: dotnet run -c Release -- --filter * --job dry
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(WithDefaults(args));

    private static string[] WithDefaults(string[] args)
    {
        List<string> result = [.. args];

        // In-process: BenchmarkDotNet's out-of-process runner needs the .csproj file name to equal
        // the assembly name (Spectara.Revela.Benchmarks.*), which the repository layout does not use.
        // Each operation takes seconds, so in-process overhead is negligible.
        if (!HasOption(args, "--inProcess", "-i"))
        {
            result.Add("--inProcess");
        }

        // Each operation encodes full photos, so 1 warmup + 3 measured iterations are enough
        // unless a job (e.g. --job dry) is chosen explicitly.
        if (!HasOption(args, "--job", "-j"))
        {
            result.AddRange(["--warmupCount", "1", "--iterationCount", "3"]);
        }

        return [.. result];
    }

    private static bool HasOption(string[] args, string longName, string shortName) =>
        args.Any(arg => arg.Equals(longName, StringComparison.OrdinalIgnoreCase)
            || arg.Equals(shortName, StringComparison.Ordinal));
}
