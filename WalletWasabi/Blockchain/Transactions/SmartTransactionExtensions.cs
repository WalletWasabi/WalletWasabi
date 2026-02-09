using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Blockchain.Transactions;

public static class SmartTransactionExtensions
{
	public static IEnumerable<Output> GetOutputs(this SmartTransaction smartTransaction, Network network)
	{
		var known = smartTransaction.WalletOutputs.Select(coin =>
		{
			var address = coin.TxOut.ScriptPubKey.GetDestinationAddress(network)!;
			return new OwnOutput(coin.TxOut.Value, address, coin.HdPubKey.IsInternal);
		}).Cast<Output>();

		var unknown = smartTransaction.ForeignOutputs.Select(coin =>
		{
			var address = coin.TxOut.ScriptPubKey.GetDestinationAddress(network)!;
			return new ForeignOutput(coin.TxOut.Value, address);
		}).Cast<Output>();

		return known.Concat(unknown);
	}

	public static IEnumerable<IInput> GetInputs(this SmartTransaction transaction)
	{
		var known = transaction.WalletInputs
			.Select(x => new KnownInput(x.Amount))
			.OfType<IInput>();

		var unknown = transaction.ForeignInputs
			.Select(_ => new ForeignInput())
			.OfType<IInput>();

		return known.Concat(unknown);
	}

	public static uint GetConfirmations(this SmartTransaction transaction, uint blockchainTipHeight)
		=> transaction.Height switch
		{
			ChainHeight(var height) => blockchainTipHeight - height + 1,
			_ => 0
		};

	public static Money? GetFee(this SmartTransaction transaction)
	{
		if (transaction.TryGetFee(out Money? fee))
		{
			return fee;
		}

		return null;
	}
	public static bool CanBeSpeedUpUsingCpfp(this SmartTransaction tx) =>
		!tx.Confirmed && (tx.ForeignInputs.Count != 0 || tx.ForeignOutputs.Count != 0);

}
