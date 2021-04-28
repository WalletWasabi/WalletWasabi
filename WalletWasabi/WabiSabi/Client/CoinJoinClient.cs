using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinClient : BackgroundService, IDisposable
	{
		private bool _disposedValue;
		private CredentialPool AmountCredentialPool { get; } = new();
		private CredentialPool VsizeCredentialPool { get; } = new();
		private Round Round { get; }
		public IArenaRequestHandler ArenaRequestHandler { get; }
		private BitcoinSecret BitcoinSecret { get; }
		private SecureRandom SecureRandom { get; }
		private List<Task> AliceClients { get; } = new();
		private object AliceClientsLock { get; } = new();
		private CancellationTokenSource DisposeCts { get; } = new();

		public CoinJoinClient(Round round, IArenaRequestHandler arenaRequestHandler, BitcoinSecret bitcoinSecret)
		{
			Round = round;
			ArenaRequestHandler = arenaRequestHandler;
			BitcoinSecret = bitcoinSecret;
			SecureRandom = new SecureRandom();
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			do
			{
				lock (AliceClientsLock)
				{
					if (AliceClients.All(t => t.IsCompleted))
					{
						// All Alice confirmed
						break;
					}
				}
			}
			while (true);
		}

		public async Task RegisterCoinAsync(Coin coin)
		{
			var aliceArenaClient = new ArenaClient(
				Round.AmountCredentialIssuerParameters,
				Round.VsizeCredentialIssuerParameters,
				AmountCredentialPool,
				VsizeCredentialPool,
				ArenaRequestHandler,
				SecureRandom);

			var aliceClient = await AliceClient.CreateNewAsync(aliceArenaClient, coin, BitcoinSecret, Round.Id, Round.Hash, Round.FeeRate).ConfigureAwait(false);

			lock (AliceClientsLock)
			{
				AliceClients.Add(aliceClient.ConfirmConnectionAsync(TimeSpan.FromSeconds(5), DisposeCts.Token));
			}

			await StartAsync(DisposeCts.Token).ConfigureAwait(false);
		}

		public async Task StopAsync()
		{
			await StopAsync().ConfigureAwait(false);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					DisposeCts.Cancel();
					SecureRandom.Dispose();
				}
				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
