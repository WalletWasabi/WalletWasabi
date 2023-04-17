using DynamicData;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletListModel
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IObservable<IWalletModel?> SelectedWallet { get; }

	void Select(IWalletModel wallet);
}
