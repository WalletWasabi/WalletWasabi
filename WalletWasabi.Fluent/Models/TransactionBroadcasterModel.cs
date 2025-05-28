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
		var nullMoney = new Money(-1L);
		var nullOutput = new TxOut(nullMoney, Script.Empty);

		var psbt = PSBT.FromTransaction(transaction.Transaction, _network);

		TxOut GetOutput(OutPoint outpoint) =>
			Services.BitcoinStore.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
				? prevTxn.Transaction.Outputs[outpoint.N]
				: nullOutput;

		var inputAddressAmount = psbt.Inputs
			.Select(x => x.PrevOut)
			.Select(GetOutput)
			.ToArray();

		var outputAddressAmount = psbt.Outputs
			.Select(x => x.GetTxOut())
			.ToArray();

		var psbtTxn = psbt.ExtractTransaction();

		var transactionId = psbtTxn.GetHash().ToString();

		var inputCount = inputAddressAmount.Length;
		var inputAmount =
			inputAddressAmount.Any(x => x.Value == nullMoney)
			? null
			: new Amount(inputAddressAmount.Select(x => x.Value).Sum());

		var outputCount = outputAddressAmount.Length;
		var outputAmount =
			outputAddressAmount.Any(x => x.Value == nullMoney)
			? null
			: new Amount(outputAddressAmount.Select(x => x.Value).Sum());

		var networkFee = inputAmount is null || outputAmount is null
			? null
			: new Amount(inputAmount.Btc - outputAmount.Btc);

		return new TransactionBroadcastInfo(transactionId, inputCount, outputCount, inputAmount, outputAmount, networkFee);
	}

	public Task SendAsync(SmartTransaction transaction)
	{
		return Services.TransactionBroadcaster.SendTransactionAsync(transaction);
	}
}
