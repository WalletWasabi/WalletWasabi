using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class InputTransactionDebugInfo
{
	private readonly KnownInput _knownInput;

	public InputTransactionDebugInfo(KnownInput knownInput)
	{
		_knownInput = knownInput;
	}

	public OutPoint OutPoint => _knownInput.Outpoint;
	public Money Amount => _knownInput.Amount;
	public uint Index => _knownInput.Index;
	public string Address => _knownInput.Address.ToString();
	public double Anonscore => _knownInput.Anonscore;
}
