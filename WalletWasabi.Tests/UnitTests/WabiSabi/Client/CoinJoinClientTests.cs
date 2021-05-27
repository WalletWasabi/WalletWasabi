using Moq;
using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Controllers;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class CoinJoinClientTests
	{
		[Fact]
		public async Task CoinJoinClientTestAsync()
		{
			var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
			var round = WabiSabiFactory.CreateRound(config);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

			var roundState = RoundState.FromRound(round);

			using var key = new Key();
			var outpoint = BitcoinFactory.CreateOutPoint();
			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					Confirmations = 200,
					TxOut = new TxOut(Money.Coins(1m), key.PubKey.WitHash.GetAddress(Network.Main)),
				});
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			ZeroCredentialPool amountCredentials = new();
			ZeroCredentialPool vsizeCredentials = new();

			string password = "whiterabbit";
			var km = ServiceFactory.CreateKeyManager(password);
			var smartCoin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1));
			Kitchen kitchen = new();
			kitchen.Cook(password);

			var wabiSabiApi = new WabiSabiController(coordinator);

			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), wabiSabiApi);
			await roundStateUpdater.StartAsync(CancellationToken.None);

			using CoinJoinClient coinJoinClient = new(wabiSabiApi, new[] { smartCoin.Coin }, kitchen, km, roundStateUpdater);
			await coinJoinClient.StartAsync(CancellationToken.None);
			await coinJoinClient.StopAsync(CancellationToken.None);
			await roundStateUpdater.StopAsync(CancellationToken.None);
		}
	}
}
