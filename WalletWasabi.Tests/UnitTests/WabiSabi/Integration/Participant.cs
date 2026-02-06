using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.Tests.UnitTests.WabiSabi.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

internal class Participant
{
	public Participant(string name, IRPCClient rpc, Func<string, IWabiSabiApiRequestHandler> apiClientFactory)
	{
		HttpClientFactory = apiClientFactory;

		Wallet = new TestWallet(name, rpc);
	}

	private TestWallet Wallet { get; }
	public Func<string, IWabiSabiApiRequestHandler> HttpClientFactory { get; }
	private SmartTransaction? SplitTransaction { get; set; }

	public async Task GenerateSourceCoinAsync(CancellationToken cancellationToken)
	{
		await Wallet.GenerateAsync(1, cancellationToken).ConfigureAwait(false);
	}

	public async Task GenerateCoinsAsync(int numberOfCoins, int seed, CancellationToken cancellationToken)
	{
		var feeRate = new FeeRate(4.0m);
		var (splitTx, spendingCoin) = Wallet.CreateTemplateTransaction();
		var availableAmount = spendingCoin.EffectiveValue(feeRate);

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
		SplitTransaction = new SmartTransaction(splitTx, new Height.ChainHeight(1));
	}

	public async Task<CoinJoinResult> StartParticipatingAsync(CancellationToken cancellationToken)
	{
		if (SplitTransaction is null)
		{
			throw new InvalidOperationException($"{nameof(GenerateCoinsAsync)} has to be called first.");
		}

		var apiClient = HttpClientFactory;
		using var roundStateUpdater = RoundStateUpdaterForTesting.Create(apiClient("satoshi"));
		await using var ticker = new Timer(_ => roundStateUpdater.Update(), 0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
		var roundStateProvider = new RoundStateProvider(roundStateUpdater);

		var outputProvider = new OutputProvider(Wallet);
		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(HttpClientFactory, Wallet, outputProvider, roundStateProvider, false);

		static HdPubKey CreateHdPubKey(ExtPubKey extPubKey)
		{
			var hdPubKey = new HdPubKey(extPubKey.PubKey, KeyPath.Parse($"m/84'/0/0/0/{extPubKey.Child}"), LabelsArray.Empty, KeyState.Clean);
			hdPubKey.SetAnonymitySet(1); // bug if not settled
			return hdPubKey;
		}

		var smartCoins = SplitTransaction.Transaction.Outputs.AsIndexedOutputs()
			.Select(x => (IndexedTxOut: x, HdPubKey: Wallet.GetExtPubKey(x.TxOut.ScriptPubKey)))
			.Select(x => new SmartCoin(SplitTransaction, x.IndexedTxOut.N, CreateHdPubKey(x.HdPubKey)))
			.ToList();

		// Run the coinjoin client task.
		var ret = await coinJoinClient.StartCoinJoinAsync(async () => await Task.FromResult(smartCoins), true, cancellationToken).ConfigureAwait(false);

		return ret;
	}
}
