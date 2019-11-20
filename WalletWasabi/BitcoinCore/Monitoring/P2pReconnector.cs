using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class P2pReconnector : PeriodicRunner
	{
		public P2pNode P2pNode { get; set; }
		private TaskCompletionSource<object> Success { get; }

		public P2pReconnector(TimeSpan period, P2pNode p2pNode) : base(period)
		{
			P2pNode = Guard.NotNull(nameof(p2pNode), p2pNode);
			Success = new TaskCompletionSource<object>();
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				Logger.LogInfo("Trying to reconnect to P2P...");
				P2pNode.Disconnect();
				await P2pNode.ConnectAsync(cancel).ConfigureAwait(false);
			}
			catch
			{
				P2pNode.Disconnect();
				throw;
			}

			Logger.LogInfo("Successfully reconnected to P2P.");

			Success.TrySetResult(null);
		}

		public async Task StartAndAwaitReconnectionAsync(CancellationToken cancel)
		{
			await StartAsync(cancel).ConfigureAwait(false);
			using var ctr = cancel.Register(() => Success.SetResult(null));
			await Success.Task.ConfigureAwait(false);

			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
				await StopAsync(cts.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
			Dispose();
		}
	}
}
