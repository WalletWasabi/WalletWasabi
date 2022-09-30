using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public interface ITransactionViewModel : ITransaction
{
	FeeRate? FeeRate { get; }
	Money? Fee { get; }
	IEnumerable<Feature> Features { get; }
}
