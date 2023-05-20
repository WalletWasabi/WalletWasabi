using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public interface IWalletNavigation
{
	IWalletViewModel? To(IWalletModel wallet);
}
