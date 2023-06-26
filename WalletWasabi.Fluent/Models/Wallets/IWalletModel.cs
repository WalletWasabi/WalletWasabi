using System.Collections.Generic;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// This interface serves the purpose of enabling Mocks for unit testing of the ViewModels that consume it.
/// It belongs to the Model part in the Model-View-ViewModel pattern
/// </summary>
public interface IWalletModel
{
	public string Name { get; }

	IObservable<WalletState> State { get; }

	IObservable<IChangeSet<ICoinModel>> Coins { get; }

	IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	IWalletBalancesModel Balances { get; }

	bool IsHardwareWallet { get; }

	bool IsWatchOnlyWallet { get; }

	IWalletAuthModel Auth { get; }

	IWalletLoadWorkflow Loader { get; }

	IWalletSettingsModel Settings { get; }

	IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels);

	IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent);
}
