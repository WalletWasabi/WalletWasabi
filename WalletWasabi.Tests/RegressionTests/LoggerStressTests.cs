using System.Diagnostics;
using WalletWasabi.Cache;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

/// <summary>
/// Stress tests for <see cref="IdempotencyRequestCache"/>.
/// </summary>
public class LoggerStressTests
{
	[Fact]
	public void StressTest()
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
}
