using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class WorkerLifecycleTests
{
	[Fact]
	public async Task DisposedWorkerCanReconnectWithoutAStaleRegistration()
	{
		var name = "mobile-reconnect-" + Guid.NewGuid().ToString("N");
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var first = Workers.Spawn<int>(name, async (_, token) =>
		{
			entered.TrySetResult();
			try { await Task.Delay(Timeout.Infinite, token); }
			finally { Assert.True(token.IsCancellationRequested); stopped.TrySetResult(); }
		});
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Throws<ArgumentException>(() => Workers.Spawn<int>(name, (_, _) => Task.CompletedTask));
		first.Dispose();
		using var second = Workers.Spawn<int>(name, async (mailbox, token) => await Task.Delay(Timeout.Infinite, token));
		// Disposing a duplicate must never unregister the actual replacement.
		first.Dispose();
		Assert.True(Workers.Tell(name, 1));
		await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}
}
