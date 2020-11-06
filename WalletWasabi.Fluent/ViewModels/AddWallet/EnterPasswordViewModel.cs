using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class EnterPasswordViewModel : RoutableViewModel
	{
		private string? _password;
		private string? _confirmPassword;

		public EnterPasswordViewModel(NavigationStateViewModel navigationState, WalletManager walletManager, BitcoinStore store, Network network, string walletName) : base(navigationState, NavigationTarget.Dialog)
		{
			// This means pressing continue will make the password empty string.
			// pressing cancel will return null.
			_password = "";

			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.ConfirmPassword, ValidateConfirmPassword);

			var nextCommandCanExecute = this.WhenAnyValue(
				x => x.Password,
				x => x.ConfirmPassword,
				(password, confirmPassword) =>
				{
					// This will fire validations before return canExecute value.
					this.RaisePropertyChanged(nameof(Password));
					this.RaisePropertyChanged(nameof(ConfirmPassword));

					return (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(confirmPassword)) || (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(confirmPassword) && !Validations.Any);
				})
				.ObserveOn(RxApp.MainThreadScheduler);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var (km, mnemonic) = await Task.Run(
						() =>
						{
							var walletGenerator = new WalletGenerator(
								walletManager.WalletDirectories.WalletsDir,
								network)
							{
								TipHeight = store.SmartHeaderChain.TipHeight
							};
							return walletGenerator.GenerateWallet(walletName, _password);
						});

					await navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(
						new RecoveryWordsViewModel(navigationState, km, mnemonic, walletManager));
				}, nextCommandCanExecute);
		}

		public string? Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string? ConfirmPassword
		{
			get => _confirmPassword;
			set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
		}

		public ICommand NextCommand { get; }

		private void ValidateConfirmPassword(IValidationErrors errors)
		{
			if (!string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword)
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.MatchingMessage);
			}
		}

		private void ValidatePassword(IValidationErrors errors)
		{
			if (PasswordHelper.IsTrimable(Password, out _))
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.WhitespaceMessage);
			}

			if (PasswordHelper.IsTooLong(Password, out _))
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage);
			}
		}
	}
}