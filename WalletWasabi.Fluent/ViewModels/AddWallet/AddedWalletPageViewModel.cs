using ReactiveUI;
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
		public AddedWalletPageViewModel(NavigationStateViewModel navigationState, string walletName, WalletType type) : base(navigationState, NavigationTarget.DialogScreen)
		{
			WalletName = walletName;
			Type = type;

			NextCommand = ReactiveCommand.Create(() =>
			{
				ClearNavigation();
			});
		}

		public WalletType Type { get; }

		public string WalletName { get; } = "";
	}
}
