using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class OutputTransactionDebugInfo
{
	private readonly KnownOutput _knownOutput;
	private readonly IEnumerable<IOutput> _allOutputs;

	public OutputTransactionDebugInfo(KnownOutput knownOutput, IEnumerable<IOutput> allOutputs)
	{
		_knownOutput = knownOutput;
		_allOutputs = allOutputs;
	}

	public string Address => _knownOutput.Destination.ToString();
	public Money Amount => _knownOutput.Amount;
	public double Anonscore => _knownOutput.Anonscore;
	public uint Index => _knownOutput.Index;
	public int EqualOutputs => _allOutputs
		.OfType<UnknownOutput>()
		.Count(x => x.Amount == Amount);
}
