using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

[AutoInterface]
public partial class TransactionBroadcasterModel
{
	private readonly Network _network;

	public TransactionBroadcasterModel(Network network)
	{
		_network = network;
	}

	public SmartTransaction? Parse(string text)
	{
		if (PSBT.TryParse(text, _network, out var signedPsbt))
		{
			if (!signedPsbt.IsAllFinalized())
			{
				signedPsbt.Finalize();
			}

			return signedPsbt.ExtractSmartTransaction();
		}
		else
		{
			return new SmartTransaction(Transaction.Parse(text, _network), Height.Unknown);
		}
	}

	public Task<SmartTransaction> LoadFromFileAsync(string filePath)
	{
		return TransactionHelpers.ParseTransactionAsync(filePath, _network);
	}

	public TransactionBroadcastInfo GetBroadcastInfo(SmartTransaction transaction)
	{
		var tx = transaction.Transaction;
		var transactionId = tx.GetHash().ToString();

		var spendingSum =
			tx.Inputs
			.Select(x => x.PrevOut)
			.Select(GetOutput)
			.Aggregate<TxOut?, Money?>(Money.Zero, (acc, txout) => (acc, txout) switch
			{
				({ } a, { } t) => a + t.Value,
				_ => null
			});

		var outputSum = tx.Outputs.Select(x => x.Value).Sum();

		var spendingAmount = spendingSum is not null ? new Amount(spendingSum) : null;
		var outputAmount = outputSum is not null ? new Amount(outputSum) : null;

		var networkFee = spendingAmount is null || outputAmount is null
			? null
			: new Amount(spendingAmount.Btc - outputAmount.Btc);

		return new TransactionBroadcastInfo(transactionId, tx.Inputs.Count, tx.Outputs.Count , spendingAmount, outputAmount, networkFee);

		TxOut? GetOutput(OutPoint outpoint) =>
			Services.BitcoinStore.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
				? prevTxn.Transaction.Outputs[outpoint.N]
				: null;
	}

	public Task SendAsync(SmartTransaction transaction)
	{
		return Services.TransactionBroadcaster.SendTransactionAsync(transaction);
	}
}
