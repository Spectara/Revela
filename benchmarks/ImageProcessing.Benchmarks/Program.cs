using BenchmarkDotNet.Running;

namespace ImageProcessing.Benchmarks;

public class Program
{
    // Run with: dotnet run -c Release -- --filter *
    // Or specific: dotnet run -c Release -- --filter *ResizeStrategy*
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
