using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitoring;

public class P2pReconnector : PeriodicRunner
{
	public P2pReconnector(TimeSpan period, P2pNode p2pNode) : base(period)
	{
		__p2pNode = Guard.NotNull(nameof(p2pNode), p2pNode);
		_success = new TaskCompletionSource<bool>();
	}

	private readonly P2pNode __p2pNode;
	private readonly TaskCompletionSource<bool> _success;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		Logger.LogInfo("Trying to reconnect to P2P...");
		if (await __p2pNode.TryDisconnectAsync(cancel).ConfigureAwait(false))
		{
			await __p2pNode.ConnectAsync(cancel).ConfigureAwait(false);

			Logger.LogInfo("Successfully reconnected to P2P.");
			_success.TrySetResult(true);
		}
	}

	public async Task StartAndAwaitReconnectionAsync(CancellationToken cancel)
	{
		await StartAsync(cancel).ConfigureAwait(false);
		using var ctr = cancel.Register(() => _success.SetResult(false));
		await _success.Task.ConfigureAwait(false);

		try
		{
			using var cts = new CancellationTokenSource(Period * 2);

			// Stop the PeriodicRunner.
			await StopAsync(cts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}
}
