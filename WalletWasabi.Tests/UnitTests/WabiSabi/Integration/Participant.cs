using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration
{
	internal class Participant
	{
		public Participant(IRPCClient rpc, WabiSabiHttpApiClient apiClient)
		{
			Rpc = rpc;
			ApiClient = apiClient;

			KeyManager = KeyManager.CreateNew(out var _, password: "");
			KeyManager.AssertCleanKeysIndexed();
		}

		public KeyManager KeyManager { get; }
		public List<Coin> Coins { get; } = new();
		public IRPCClient Rpc { get; }
		public WabiSabiHttpApiClient ApiClient { get; }

		public CoinJoinClient? CoinJoinClient { get; set; }

		public RoundStateUpdater? RoundStateUpdater { get; set; }

		public async Task InitializeAsync(int numberOfCoins, CancellationToken cancellationToken)
		{
			var keys = KeyManager.GetKeys().Take(numberOfCoins);
			foreach (var key in keys)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var blockIds = await Rpc.GenerateToAddressAsync(1, key.GetP2wpkhAddress(Rpc.Network));
				var block = await Rpc.GetBlockAsync(blockIds.First());
				var coin = block.Transactions[0].Outputs.GetCoins(key.P2wpkhScript).First();
				Coins.Add(coin);
			}
		}

		public async Task StartParticipatingAsync(CancellationToken cancellationToken)
		{
			try
			{
				RoundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(3), ApiClient);
				await RoundStateUpdater.StartAsync(cancellationToken).ConfigureAwait(false);

				var kitchen = new Kitchen();
				kitchen.Cook("");

				CoinJoinClient = new CoinJoinClient(ApiClient, Coins, kitchen, KeyManager, RoundStateUpdater);

				// Run the coinjoin client task.
				await CoinJoinClient.StartCoinJoinAsync(cancellationToken).ConfigureAwait(false);

				await RoundStateUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				RoundStateUpdater?.Dispose();
			}
		}
	}
}
