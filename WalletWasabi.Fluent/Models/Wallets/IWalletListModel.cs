using DynamicData;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletListModel
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IWalletModel? DefaultWallet { get; }

	IWalletModel? SelectedWallet { get; set; }
}
