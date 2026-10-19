using BenchmarkDotNet.Running;

// Always run in Release, from the solution folder:
//   dotnet run -c Release --project benchmarks/ToolsApp.Benchmarks -- --filter *
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
