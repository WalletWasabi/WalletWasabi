using DynamicData;

namespace WalletWasabi.Fluent.Models.Wallets;

internal interface IWalletListModel
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }
}
