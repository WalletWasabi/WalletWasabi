using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public enum WalletType
	{
		Normal,
		Hardware,
		Coldcard,
		Trezor,
		Ledger
	}

	public class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(string walletName, WalletType type)
		{
			WalletName = walletName;
			Type = type;

			NextCommand = ReactiveCommand.Create(() =>
			{
				Navigate().Clear();
			});
		}

		public AddedWalletPageViewModel(NavBarViewModel navBar, string walletName, WalletType type)
		{
			WalletName = walletName;
			Type = type;

			NextCommand = ReactiveCommand.Create(() =>
			{
				var targetWallet = navBar.Items.FirstOrDefault(x => x.WalletName == walletName);

				if (targetWallet is not null)
				{
					navBar.SelectedItem = targetWallet;
				}
			});
		}

		public WalletType Type { get; }

		public string WalletName { get; }
	}
}
