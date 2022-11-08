using System.Diagnostics;
using WalletWasabi.Cache;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

/// <summary>
/// Stress tests for <see cref="IdempotencyRequestCache"/>.
/// </summary>
public class LoggerPerfTests
{
	[Fact]
	public void NoLoggingPerfTest()
	{
		const int IterationCount = 1_000_000;

		Stopwatch sw1 = Stopwatch.StartNew();

		for (int i = 0; i < IterationCount; i++)
		{
			Logger.LogTrace(message: $"{new string((char)i, 500)}");
		}

		sw1.Stop();
		Stopwatch sw2 = Stopwatch.StartNew();

		for (int i = 0; i < IterationCount; i++)
		{
			string message = $"{new string((char)i, 500)}";
			Logger.LogTrace(message);
		}

		sw2.Stop();
		Debug.WriteLine(sw1.ElapsedMilliseconds);
		Debug.WriteLine(sw2.ElapsedMilliseconds);
		Debug.WriteLine("DONE");
	}

	[Fact]
	public void FileLoggingPerfTest()
	{
		const int IterationCount = 10_000;

		Logger.Initialize(isEnabled: true, "C:\\temp\\stressTest.log", minimumLogLevel: LogLevel.Debug, LogMode.File);
		Stopwatch sw1 = Stopwatch.StartNew();

		for (int i = 0; i < IterationCount; i++)
		{
			Logger.LogDebug(message: "Test message");
		}

		sw1.Stop();

		// 2s without optimizations.
		Debug.WriteLine(sw1.ElapsedMilliseconds);
		Debug.WriteLine("DONE");
	}
}
