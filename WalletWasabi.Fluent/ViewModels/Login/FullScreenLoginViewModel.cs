using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public class FullScreenLoginViewModel : LoginViewModelBase
	{
		public FullScreenLoginViewModel(WalletManager walletManager, ObservableCollection<WalletViewModelBase> wallets)
			: base(walletManager)
		{
			Wallets = wallets;
			SelectedWallet = wallets.First();
		}

		public ObservableCollection<WalletViewModelBase> Wallets { get; }
	}
}