using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Enter your password")]
	public partial class PasswordAuthDialogViewModel : DialogViewModelBase<SmartTransaction?>
	{
		[AutoNotify] private string _password;

		public PasswordAuthDialogViewModel(Wallet wallet, SmartTransaction transaction)
		{
			_password = "";

			var canExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back), canExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel), canExecute);
			NextCommand = ReactiveCommand.CreateFromTask(async () =>
				{
					var passwordValid = await Task.Run(() => PasswordHelper.TryPassword(wallet.KeyManager, Password, out _));

					if (passwordValid)
					{
						Close(DialogResultKind.Normal, transaction);
					}
					else
					{
						await ShowErrorAsync("Password", "Password was incorrect.", "");
						Close();
					}
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
