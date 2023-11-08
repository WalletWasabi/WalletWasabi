using BenchmarkDotNet.Running;
using WalletWasabi.Benchmarks.JsonConverters;

var summary = BenchmarkRunner.Run<TimeSpanConverter>();
