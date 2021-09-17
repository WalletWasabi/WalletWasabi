using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration
{
	internal class Participant
	{
		public Participant(IRPCClient rpc, IBackendHttpClientFactory httpClientFactory)
		{
			Rpc = rpc;
			HttpClientFactory = httpClientFactory;

			KeyManager = KeyManager.CreateNew(out var _, password: "");
			KeyManager.AssertCleanKeysIndexed();
		}

		public KeyManager KeyManager { get; }
		public List<SmartCoin> Coins { get; } = new();
		public IRPCClient Rpc { get; }
		public IBackendHttpClientFactory HttpClientFactory { get; }

		private Coin? SourceCoin { get; set; }

		public async Task GenerateSourceCoinAsync(CancellationToken cancellationToken)
		{
			var minerKey = KeyManager.GetNextReceiveKey("coinbase", out _);
			var blockIds = await Rpc.GenerateToAddressAsync(1, minerKey.GetP2wpkhAddress(Rpc.Network)).ConfigureAwait(false);
			var block = await Rpc.GetBlockAsync(blockIds.First()).ConfigureAwait(false);
			SourceCoin = block.Transactions[0].Outputs.GetCoins(minerKey.P2wpkhScript).First();
			minerKey.SetKeyState(KeyState.Used);
		}

		public async Task GenerateCoinsAsync(int numberOfCoins, int seed, CancellationToken cancellationToken)
		{
			var feeRate = new FeeRate(4.0m);
			var splitTx = Transaction.Create(Rpc.Network);
			splitTx.Inputs.Add(new TxIn(SourceCoin!.Outpoint));

			var rnd = new Random(seed);
			double NextNotTooSmall() => 0.00001 + (rnd.NextDouble() * 0.99999);
			var sampling = Enumerable
				.Range(0, numberOfCoins - 1)
				.Select(_ => NextNotTooSmall())
				.Prepend(0)
				.Prepend(1)
				.OrderBy(x => x)
				.ToArray();

			var amounts = sampling
				.Zip(sampling.Skip(1), (x, y) => y - x)
				.Select(x => x * SourceCoin.Amount.Satoshi)
				.Select(x => Money.Satoshis((long)x));

			var keys = Enumerable.Range(0, amounts.Count()).Select(x => KeyManager.GetNextReceiveKey("no-label", out _)).ToImmutableList();
			foreach (var (amount, key) in amounts.Zip(keys))
			{
				var scriptPubKey = key.P2wpkhScript;
				var effectiveOutputValue = amount - feeRate.GetFee(scriptPubKey.EstimateOutputVsize());
				splitTx.Outputs.Add(new TxOut(effectiveOutputValue, scriptPubKey));
			}
			var minerKey = KeyManager.GetSecrets("", SourceCoin.ScriptPubKey).First();
			splitTx.Sign(minerKey.PrivateKey.GetBitcoinSecret(Rpc.Network), SourceCoin);
			var stx = new SmartTransaction(splitTx, new Height(500_000));

			var smartCoins = keys.Select((k, i) => new SmartCoin(stx, (uint)i, k));
			Coins.AddRange(smartCoins);
			await Rpc.SendRawTransactionAsync(splitTx).ConfigureAwait(false);
		}

		public async Task StartParticipatingAsync(CancellationToken cancellationToken)
		{
			var apiClient = new WabiSabiHttpApiClient(HttpClientFactory.NewBackendHttpClient(Mode.DefaultCircuit));
			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(3), apiClient);
			await roundStateUpdater.StartAsync(cancellationToken).ConfigureAwait(false);

			var kitchen = new Kitchen();
			kitchen.Cook("");

			var coinJoinClient = new CoinJoinClient(HttpClientFactory, kitchen, KeyManager, roundStateUpdater);

			// Run the coinjoin client task.
			await coinJoinClient.StartCoinJoinAsync(Coins, cancellationToken).ConfigureAwait(false);

			await roundStateUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
