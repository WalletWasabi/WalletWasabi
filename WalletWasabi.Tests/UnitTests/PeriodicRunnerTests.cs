using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
			public AutoResetEvent NextRoundAutoResetEvent { get; private set; } = new AutoResetEvent(false);

			public int RoundCounter { get; private set; }

			internal TestRunner(TimeSpan period) : base(period)
			{
			}

			protected override async Task ActionAsync(CancellationToken cancel)
			{
				// Add some delay to simulate work.
				await Task.Delay(50).ConfigureAwait(false);
				RoundCounter++;
				NextRoundAutoResetEvent.Set();
			}
		}

		[Fact]
		public async Task PeriodicRunnerTestsAsync()
		{
			var period = TimeSpan.FromSeconds(1);
			var failTimeout = TimeSpan.FromSeconds(5);
			var additionalDelay = TimeSpan.FromSeconds(0.5);

			using var runner = new TestRunner(period);
			Stopwatch sw = new Stopwatch();
			using CancellationTokenSource cts = new CancellationTokenSource();

			// First round starts immediately.

			await runner.StartAsync(cts.Token);
			sw.Start();
			Assert.True(runner.NextRoundAutoResetEvent.WaitOne(failTimeout));
			Assert.Equal(1, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, TimeSpan.Zero, TimeSpan.Zero + additionalDelay);

			// Second round start only after Period is elapsed.
			Assert.True(runner.NextRoundAutoResetEvent.WaitOne(failTimeout));
			Assert.Equal(2, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) + additionalDelay);

			// Third round start only after Period is elapsed.
			Assert.True(runner.NextRoundAutoResetEvent.WaitOne(failTimeout));
			Assert.Equal(3, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) + additionalDelay);

			// If Trigger called then run immediately.
			runner.TriggerRound();
			// Triggering a round meanwhile it is running should not be a problem.
			runner.TriggerRound();
			runner.TriggerRound();
			runner.TriggerRound();
			runner.TriggerRound();
			Assert.True(runner.NextRoundAutoResetEvent.WaitOne(failTimeout));
			Assert.Equal(4, runner.RoundCounter);
			Assert.InRange(sw.Elapsed, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) + additionalDelay);

			// If Trigger was called during the actual run it should one more time but only once!
			await Task.Delay(period / 2);
			Assert.Equal(5, runner.RoundCounter);
		}
	}
}
;
