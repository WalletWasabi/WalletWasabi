using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Models;

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

	public static IEnumerable<BitcoinAddress> GetDestinationAddresses(this SmartTransaction transaction, Network network, out List<IInput> inputs, out List<Output> outputs)
	{
		inputs = transaction.GetInputs().ToList();
		outputs = transaction.GetOutputs(network).ToList();

		return GetDestinationAddresses(inputs, outputs);
	}

	private static IEnumerable<BitcoinAddress> GetDestinationAddresses(ICollection<IInput> inputs, ICollection<Output> outputs)
	{
		var myOwnInputs = inputs.OfType<KnownInput>().ToList();
		var foreignInputs = inputs.OfType<ForeignInput>().ToList();
		var myOwnOutputs = outputs.OfType<OwnOutput>().ToList();
		var foreignOutputs = outputs.OfType<ForeignOutput>().ToList();

		// All inputs and outputs are my own, transaction is a self-spend.
		if (!foreignInputs.Any() && !foreignOutputs.Any())
		{
			// Classic self-spend to one or more external addresses.
			if (myOwnOutputs.Any(x => !x.IsInternal))
			{
				// Destinations are the external addresses.
				return myOwnOutputs.Where(x => !x.IsInternal).Select(x => x.DestinationAddress);
			}

			// Edge-case: self-spend to one or more internal addresses.
			// We can't know the destinations, return all the outputs.
			return myOwnOutputs.Select(x => x.DestinationAddress);
		}

		// All inputs are foreign but some outputs are my own, someone is sending coins to me.
		if (!myOwnInputs.Any() && myOwnOutputs.Any())
		{
			// All outputs that are my own are the destinations.
			return myOwnOutputs.Select(x => x.DestinationAddress);
		}

		// I'm sending a transaction to someone else.
		// All outputs that are not my own are the destinations.
		return foreignOutputs.Select(x => x.DestinationAddress);
	}

	public static int GetConfirmations(this SmartTransaction transaction, int blockchainTipHeight)
		=> transaction.Height.Type == HeightType.Chain ? blockchainTipHeight - transaction.Height.Value + 1 : 0;
}
