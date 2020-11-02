using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.AddWallet.CreateWallet;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.AddWallet.Common
{
	public class EnterPasswordViewModel : ViewModelBase, IRoutableViewModel
	{
		private string _password;
		private string _confirmPassword;

		public EnterPasswordViewModel(IScreen screen, Global global, string walletName)
		{
			HostScreen = screen;

			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.ConfirmPassword, ValidateConfirmPassword);

			var continueCommandCanExecute = this.WhenAnyValue(
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

			ContinueCommand = ReactiveCommand.Create(
				() =>
				{
					var walletGenerator = new WalletGenerator(global.WalletManager.WalletDirectories.WalletsDir, global.Network);
					walletGenerator.TipHeight = global.BitcoinStore.SmartHeaderChain.TipHeight;
					var (km, mnemonic) = walletGenerator.GenerateWallet(walletName, Password);
					screen.Router.Navigate.Execute(new RecoveryWordsViewModel(screen, km, mnemonic, global));
				},
				continueCommandCanExecute);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string ConfirmPassword
		{
			get => _confirmPassword;
			set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
		}

		public ICommand ContinueCommand { get; }
		public ICommand CancelCommand { get; }

		public string? UrlPathSegment => throw new NotImplementedException();
		public IScreen HostScreen { get; }

		private void ValidateConfirmPassword(IValidationErrors errors)
		{
			if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword)
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