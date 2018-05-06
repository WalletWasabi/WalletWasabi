using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
    public class CcjClient
    {
		public Network Network { get; }

		public AliceClient AliceClient { get; }
		public BobClient BobClient { get; }
		public SatoshiClient SatoshiClient { get; }

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public CcjClient(Network network, Uri ccjHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			AliceClient = new AliceClient(ccjHostUri, torSocks5EndPoint);
			BobClient = new BobClient(ccjHostUri, torSocks5EndPoint);
			SatoshiClient = new SatoshiClient(ccjHostUri, torSocks5EndPoint);

			_running = 0;
		}

		public void Start()
		{
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							// If stop was requested return.
							if (IsRunning == false) return;
						}
						catch (Exception ex)
						{
							Logger.LogError<CcjClient>(ex);
						}
					}
				}
				finally
				{
					if (IsStopping)
					{
						Interlocked.Exchange(ref _running, 3);
					}
				}
			});
		}

		public async Task StopAsync()
		{
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			while (IsStopping)
			{
				await Task.Delay(50);
			}

			SatoshiClient?.Dispose();
			BobClient?.Dispose();
			AliceClient?.Dispose();
		}
	}
}
