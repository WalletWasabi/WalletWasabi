using DynamicData;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletListModel
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IWalletModel? DefaultWallet { get; }

	void StoreLastSelectedWallet(IWalletModel wallet);
}
