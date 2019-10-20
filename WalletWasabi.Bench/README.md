# WalletWasabi.Bench

This project uses BenchmarkDotnet to measure performance of parts of Wasabi implementation.

## Analyzing performance in Windows:

You can generate flamegraph to view with PerfView on Windows with the following command:
```powershell
dotnet run -c Release -- --runtimes netcoreapp2.2 --filter TransactionProcessorBench --profiler ETW
```

Where `TransactionProcessorBench` is the name of the benchmark class you are insterested in.

## Generating flamegraph on linux:


```
COMPlus_PerfMapEnabled=1 dotnet ~/GitHub/WalletWasabi/WalletWasabi.Bench/bin/Debug/netcoreapp3.0/WalletWasabi.Bench.dll &
sudo perf record -p $! -g
sudo perf script -f | ~/GitHub/FlameGraph/stackcollapse-perf.pl | ~/GitHub/FlameGraph/flamegraph.pl > flame.svg
sudo python -m SimpleHTTPServer 80 &
firefox http://127.0.0.1:80/
``` 

And there you can pick the flamegraph.
