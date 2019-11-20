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
		private bool Success { get; set; }

		public P2pReconnector(TimeSpan period, P2pNode p2pNode) : base(period)
		{
			P2pNode = Guard.NotNull(nameof(p2pNode), p2pNode);
			Success = false;
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

			Success = true;
		}

		public async Task StartAndAwaitReconnectionAsync(CancellationToken cancel)
		{
			await StartAsync(cancel).ConfigureAwait(false);
			try
			{
				while (!Success)
				{
					await Task.Delay(100, cancel).ConfigureAwait(false);
				}
			}
			catch (TaskCanceledException ex)
			{
				Logger.LogTrace(ex);
			}

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
