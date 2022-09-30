using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class OutputViewModel
{
	public Money Amount { get; }
	public string Address { get; }
	public bool IsSpent { get; }
	public IEnumerable<Feature> Features { get; }

	public OutputViewModel(Money amount, string address, bool isSpent, IEnumerable<Feature> features)
	{
		Amount = amount;
		Address = address;
		IsSpent = isSpent;
		Features = features;
	}
}
