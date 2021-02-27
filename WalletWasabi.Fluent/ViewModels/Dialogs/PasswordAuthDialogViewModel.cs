using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Enter your password")]
	public partial class PasswordAuthDialogViewModel : DialogViewModelBase<bool>
	{
		[AutoNotify] private string _password;

		public PasswordAuthDialogViewModel(Wallet wallet)
		{
			_password = "";

			var canExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back), canExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel), canExecute);
			NextCommand = ReactiveCommand.CreateFromTask(async () =>
				{
					var passwordValid = await Task.Run(() => PasswordHelper.TryPassword(wallet.KeyManager, Password, out _));

					if (!passwordValid)
					{
						await ShowErrorAsync("Password", "Password was incorrect.", "");
					}

					Close(DialogResultKind.Normal, passwordValid);
				}
				,canExecute);

			EnableAutoBusyOn(NextCommand);
		}

		protected override void OnDialogClosed()
		{
			Password = "";
		}
	}
}
