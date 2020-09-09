using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using Xunit;

namespace WalletWasabi.Tests.PeriodicRunnerTests
{
	/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
	[Collection("Serial unit tests collection")]
	public class PeriodicRunnerTests
	{
		internal class TestRunner : PeriodicRunner
		{
			internal TestRunner(TimeSpan period, TimeSpan maxNextRoundWaitTime) : base(period)
			{
				MaxNextRoundWaitTime = maxNextRoundWaitTime;
			}

			public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(initialCount: 0, maxCount: 1);

			public int RoundCounter { get; private set; }
			private TimeSpan MaxNextRoundWaitTime { get; }

			protected override async Task ActionAsync(CancellationToken cancel)
			{
				// Add some delay to simulate work.
				await Task.Delay(50, cancel).ConfigureAwait(false);
				RoundCounter++;

				Semaphore.Release();
			}

			public async Task<bool> WaitForNextRoundAsync()
			{
				return await Semaphore.WaitAsync(MaxNextRoundWaitTime);
			}
		}

		[Fact]
		public async Task PeriodicRunnerTestsAsync()
		{
			const double Scaler = 5.0;
			TimeSpan leniencyThreshold = Scaler * TimeSpan.FromSeconds(0.5);
			TimeSpan period = Scaler * TimeSpan.FromSeconds(1);

			using var runner = new TestRunner(period: period, maxNextRoundWaitTime: TimeSpan.FromSeconds(Scaler * 2));
			using var cts = new CancellationTokenSource();

			var sw = new Stopwatch();
			sw.Start();

			// Round #1. This round starts immediately.
			Task runnerTask = runner.StartAsync(cts.Token);

			Assert.True(await runner.WaitForNextRoundAsync());
			Assert.Equal(1, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, TimeSpan.Zero, 1 * runner.Period + leniencyThreshold); // Full period must elapse.

			// Round #2.
			Assert.True(await runner.WaitForNextRoundAsync());
			Assert.Equal(2, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, 1 * runner.Period, 2 * runner.Period + leniencyThreshold); // Full period must elapse.

			// Round #3.
			Assert.True(await runner.WaitForNextRoundAsync());
			Assert.Equal(3, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, 2 * runner.Period, 3 * runner.Period + leniencyThreshold); // Full period must elapse.

			// Run immediately next round when trigger is called.
			runner.TriggerRound();

			// Repeated triggers should do nothing when not in waiting state.
			runner.TriggerRound();
			runner.TriggerRound();
			runner.TriggerRound();
			runner.TriggerRound();

			// Round #4.
			Assert.True(await runner.WaitForNextRoundAsync());
			Assert.Equal(4, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, 2 * runner.Period, 3 * runner.Period + leniencyThreshold); // Elapsed time should not change much from the last round.

			await runner.StopAsync(cts.Token);
			await runnerTask;
		}
	}
}
