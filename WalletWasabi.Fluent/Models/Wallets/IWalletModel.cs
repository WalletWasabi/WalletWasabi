using System.Collections.Generic;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// This interface serves the purpose of enabling Mocks for unit testing of the ViewModels that consume it.
/// It belongs to the Model part in the Model-View-ViewModel pattern
/// </summary>
public interface IWalletModel
{
	public string Name { get; }

	IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	IAddress CreateReceiveAddress(IEnumerable<string> destinationLabels);

	IObservable<Money> Balance { get; }

	IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent);

	bool IsHardwareWallet();
}
