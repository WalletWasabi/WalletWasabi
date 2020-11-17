using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class P2pReconnector : PeriodicRunner
	{
		public P2pReconnector(TimeSpan period, P2pNode p2pNode) : base(period)
		{
			P2pNode = Guard.NotNull(nameof(p2pNode), p2pNode);
			Success = new TaskCompletionSource<bool>();
		}

		public P2pNode P2pNode { get; }
		private TaskCompletionSource<bool> Success { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			Logger.LogInfo("Trying to reconnect to P2P...");
			if (await P2pNode.TryDisconnectAsync(cancel).ConfigureAwait(false))
			{
				await P2pNode.ConnectAsync(cancel).ConfigureAwait(false);

				Logger.LogInfo("Successfully reconnected to P2P.");
				Success.TrySetResult(true);
			}
		}

		public async Task StartAndAwaitReconnectionAsync(CancellationToken cancel)
		{
			await StartAsync(cancel).ConfigureAwait(false);
			using var ctr = cancel.Register(() => Success.SetResult(false));
			await Success.Task.ConfigureAwait(false);

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
}
