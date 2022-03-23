using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

internal class Participant
{
	public Participant(string name, IRPCClient rpc, IWasabiHttpClientFactory httpClientFactory)
	{
		HttpClientFactory = httpClientFactory;

		Wallet = new TestWallet(name, rpc);
	}

	private TestWallet Wallet { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	private SmartTransaction? SplitTransaction { get; set; }

	public async Task GenerateSourceCoinAsync(CancellationToken cancellationToken)
	{
		await Wallet.GenerateAsync(1, cancellationToken).ConfigureAwait(false);
	}

	public async Task GenerateCoinsAsync(int numberOfCoins, int seed, CancellationToken cancellationToken)
	{
		var feeRate = new FeeRate(4.0m);
		var (splitTx, spendingCoin) = Wallet.CreateTemplateTransaction();
		var availableAmount = spendingCoin.EffectiveValue(feeRate, CoordinationFeeRate.Zero);

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
			.Select(x => x * availableAmount.Satoshi)
			.Select(x => Money.Satoshis((long)x));

		foreach (var amount in amounts)
		{
			var outputAddress = Wallet.CreateNewAddress();
			var effectiveOutputValue = amount - feeRate.GetFee(outputAddress.ScriptPubKey.EstimateOutputVsize());
			splitTx.Outputs.Add(new TxOut(effectiveOutputValue, Wallet.CreateNewAddress()));
		}
		await Wallet.SendRawTransactionAsync(Wallet.SignTransaction(splitTx), cancellationToken).ConfigureAwait(false);
		SplitTransaction = new SmartTransaction(splitTx, new Height(1));
	}

	public async Task StartParticipatingAsync(CancellationToken cancellationToken)
	{
		if (SplitTransaction is null)
		{
			throw new InvalidOperationException($"{nameof(GenerateCoinsAsync)} has to be called first.");
		}

		var apiClient = new WabiSabiHttpApiClient(HttpClientFactory.NewHttpClientWithDefaultCircuit());
		using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(3), apiClient);
		await roundStateUpdater.StartAsync(cancellationToken).ConfigureAwait(false);

		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(HttpClientFactory, Wallet, Wallet, roundStateUpdater);

		static HdPubKey CreateHdPubKey(ExtPubKey extPubKey)
		{
			var hdPubKey = new HdPubKey(extPubKey.PubKey, KeyPath.Parse($"m/84'/0/0/0/{extPubKey.Child}"), SmartLabel.Empty, KeyState.Clean);
			hdPubKey.SetAnonymitySet(1); // bug if not settled
			return hdPubKey;
		}

		var smartCoins = SplitTransaction.Transaction.Outputs.AsIndexedOutputs()
		 	.Select(x => (IndexedTxOut: x, HdPubKey: Wallet.GetExtPubKey(x.TxOut.ScriptPubKey)))
			.Select(x => new SmartCoin(SplitTransaction, x.IndexedTxOut.N, CreateHdPubKey(x.HdPubKey)))
			.ToList();

		// Run the coinjoin client task.
		await coinJoinClient.StartCoinJoinAsync(smartCoins, cancellationToken).ConfigureAwait(false);

		await roundStateUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
	}
}
