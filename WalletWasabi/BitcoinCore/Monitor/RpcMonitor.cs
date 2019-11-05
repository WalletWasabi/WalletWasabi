using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitor
{
	public class RpcMonitor : NotifyPropertyChangedBase
	{
		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		private CancellationTokenSource Stop { get; set; }

		private RpcStatus _status;

		public RpcStatus Status
		{
			get => _status;
			private set => RaiseAndSetIfChanged(ref _status, value);
		}

		public RpcMonitor()
		{
			_running = 0;
			Stop = new CancellationTokenSource();
		}

		public void Start(RPCClient rpc, TimeSpan period)
		{
			rpc = Guard.NotNull(nameof(rpc), rpc);
			if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
			{
				return;
			}

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							var bci = await rpc.GetBlockchainInfoAsync().ConfigureAwait(false);
							var pi = await rpc.GetPeersInfoAsync().ConfigureAwait(false);

							Status = RpcStatus.Responsive(bci.Headers, bci.Blocks, pi.Length);
						}
						catch (Exception ex)
						{
							Status = RpcStatus.Unresponsive;
							Logger.LogError(ex);
						}
						finally
						{
							await Task.Delay(period, Stop.Token);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Stop?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}
			Stop?.Dispose();
			Stop = null;
		}
	}
}
