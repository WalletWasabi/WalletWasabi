using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class PeriodicRunnerTests
	{
		internal class TestRunner : PeriodicRunner
		{
			internal TestRunner(TimeSpan period, TimeSpan maxNextRoundWaitTime) : base(period)
			{
				MaxNextRoundWaitTime = maxNextRoundWaitTime;
			}

			public AutoResetEvent NextRoundAutoResetEvent { get; private set; } = new AutoResetEvent(false);

			public int RoundCounter { get; private set; }
			private TimeSpan MaxNextRoundWaitTime { get; }

			protected override async Task ActionAsync(CancellationToken cancel)
			{
				// Add some delay to simulate work.
				await Task.Delay(50, cancel);
				RoundCounter++;
				NextRoundAutoResetEvent.Set();
			}

			public bool WaitForNextRound()
			{
				return NextRoundAutoResetEvent.WaitOne(MaxNextRoundWaitTime);
			}
		}

		[Fact]
		public async Task PeriodicRunnerTestsAsync()
		{
			const double Scaler = 5.0;
			TimeSpan leniencyThreshold = Scaler * TimeSpan.FromSeconds(0.5);
			TimeSpan period = Scaler * TimeSpan.FromSeconds(1);

			using var runner = new TestRunner(period: period, maxNextRoundWaitTime: TimeSpan.FromSeconds(10));
			using CancellationTokenSource cts = new CancellationTokenSource();

			var sw = new Stopwatch();
			sw.Start();

			// Round #1. This round starts immediately.
			await runner.StartAsync(cts.Token);

			Assert.True(runner.WaitForNextRound());
			Assert.Equal(1, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, TimeSpan.Zero, 1 * runner.Period + leniencyThreshold); // Full period must elapse.

			// Round #2.
			Assert.True(runner.WaitForNextRound());
			Assert.Equal(2, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, 1 * runner.Period, 2 * runner.Period + leniencyThreshold); // Full period must elapse.

			// Round #3.
			Assert.True(runner.WaitForNextRound());
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
			Assert.True(runner.WaitForNextRound());
			Assert.Equal(4, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, 2 * runner.Period, 3 * runner.Period + leniencyThreshold); // Elapsed time should not change much from the last round.

			await runner.StopAsync(cts.Token);
		}
	}
}