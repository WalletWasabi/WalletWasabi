using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public interface IWalletNavigation
{
	IWalletViewModel? To(IWalletModel wallet);
}

public interface IWalletSelector : IWalletNavigation
{
	IWalletViewModel? SelectedWallet { get; }

	IWalletModel? SelectedWalletModel { get; }
}
