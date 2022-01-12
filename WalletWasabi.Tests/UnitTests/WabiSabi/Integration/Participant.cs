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

			KeyManager = KeyManager.CreateNew(out var _, password: "", Network.Main);
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
			var blockIds = await Rpc.GenerateToAddressAsync(1, minerKey.GetP2wpkhAddress(Rpc.Network), cancellationToken).ConfigureAwait(false);
			var block = await Rpc.GetBlockAsync(blockIds.First(), cancellationToken).ConfigureAwait(false);
			SourceCoin = block.Transactions[0].Outputs.GetCoins(minerKey.P2wpkhScript).First();
			minerKey.SetKeyState(KeyState.Used);
		}

		public async Task GenerateCoinsAsync(int numberOfCoins, int seed, CancellationToken cancellationToken)
		{
			var rnd = new Random(seed);
			var feeRate = new FeeRate(4.0m);

			var splitTx = Transaction.Create(Rpc.Network);
			splitTx.Inputs.Add(new TxIn(SourceCoin!.Outpoint));

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

			var coinCreationTasks = amounts.Select(x => Task.Run(() => CreateKeyAndTxOutPair(x)));

			(HdPubKey Key, TxOut TxOut) CreateKeyAndTxOutPair(Money amount)
			{
				var key = KeyManager.GetNextReceiveKey("no-label", out _);

				key.SetAnonymitySet(1);

				var scriptPubKey = key.P2wpkhScript;
				var effectiveOutputValue = amount - feeRate.GetFee(scriptPubKey.EstimateOutputVsize());

				return (key, new TxOut(effectiveOutputValue, scriptPubKey));
			}

			var keyAndTxOutPairs = await Task.WhenAll(coinCreationTasks).ConfigureAwait(false);
			var indexedKeyAndTxOutPairs = keyAndTxOutPairs.Select((x, i) => (Index: i, Key: x.Key, TxOut: x.TxOut));
			splitTx.Outputs.AddRange(keyAndTxOutPairs.Select(x => x.TxOut));

			var minerKey = KeyManager.GetSecrets("", SourceCoin.ScriptPubKey).First();
			splitTx.Sign(minerKey.PrivateKey.GetBitcoinSecret(Rpc.Network), SourceCoin);
			var stx = new SmartTransaction(splitTx, new Height(500_000));

			Coins.AddRange(indexedKeyAndTxOutPairs.Select(x => new SmartCoin(stx, (uint)x.Index, x.Key)));

			await Rpc.SendRawTransactionAsync(splitTx, cancellationToken).ConfigureAwait(false);
		}

		public async Task StartParticipatingAsync(CancellationToken cancellationToken)
		{
			var apiClient = new WabiSabiHttpApiClient(HttpClientFactory.NewBackendHttpClient(Mode.DefaultCircuit));
			using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(3), apiClient);
			await roundStateUpdater.StartAsync(cancellationToken).ConfigureAwait(false);

			var kitchen = new Kitchen();
			kitchen.Cook("");

			var coinJoinClient = new CoinJoinClient(HttpClientFactory, kitchen, KeyManager, roundStateUpdater, consolidationMode: true);

			// Run the coinjoin client task.
			await coinJoinClient.StartCoinJoinAsync(Coins, cancellationToken).ConfigureAwait(false);

			await roundStateUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
