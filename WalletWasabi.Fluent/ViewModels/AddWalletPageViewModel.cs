using Avalonia.Input;
using ReactiveUI;
using WalletWasabi.Gui.Validation;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";

		public AddWalletPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Add Wallet";
		}

		public override string IconName => "add_circle_regular";		

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}
	}
}
