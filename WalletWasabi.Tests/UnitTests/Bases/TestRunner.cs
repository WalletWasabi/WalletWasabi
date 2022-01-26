using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.Tests.UnitTests.Bases;

public class TestRunner : PeriodicRunner
{
	public TestRunner(TimeSpan period, TimeSpan maxNextRoundWaitTime) : base(period)
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
