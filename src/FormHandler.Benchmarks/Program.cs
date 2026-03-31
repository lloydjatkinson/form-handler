using BenchmarkDotNet.Running;

namespace FormHandler.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] arguments) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments);
    }
}