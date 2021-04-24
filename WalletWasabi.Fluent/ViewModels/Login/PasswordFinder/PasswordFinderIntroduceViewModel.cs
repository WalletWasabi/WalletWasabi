using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	[NavigationMetaData(Title = "Password Finder")]
	public partial class PasswordFinderIntroduceViewModel : RoutableViewModel
	{
		public PasswordFinderIntroduceViewModel(Wallet wallet)
		{
			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			EnableBack = false;

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNext(wallet));
		}

		private async Task OnNext(Wallet wallet)
		{
			var dialogResult =
				await NavigateDialog(
					new CreatePasswordDialogViewModel("Type in your most likely password", enableEmpty: false));

			if (dialogResult.Result is { } password)
			{
				var options = new PasswordFinderOptions(wallet, password);
				Navigate().To(new SelectCharsetViewModel(options));
			}

			if (dialogResult.Kind == DialogResultKind.Cancel)
			{
				Navigate().Clear();
			}
		}
	}
}
