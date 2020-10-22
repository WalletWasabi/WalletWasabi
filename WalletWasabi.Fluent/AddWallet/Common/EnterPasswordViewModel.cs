using Microsoft.AspNetCore.Server.IIS.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.AddWallet.CreateWallet;
using WalletWasabi.Fluent.ViewModels;
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
		private string _errorMessage;
		private bool _isPasswordConfirmed;

		public EnterPasswordViewModel(IScreen screen, Global global, string walletName)
		{
			HostScreen = screen;

			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.ConfirmPassword, ValidatePassword);

			ContinueCommand = ReactiveCommand.Create(() =>
			{
				var walletGenerator = new WalletGenerator(global.WalletManager.WalletDirectories.WalletsDir, global.Network);
				walletGenerator.TipHeight = global.BitcoinStore.SmartHeaderChain.TipHeight;

				var (km, mnemonic) = walletGenerator.GenerateWallet(walletName, Password);

				screen.Router.Navigate.Execute(new RecoveryWordsViewModel(screen, km, mnemonic, global));
			});

			CancelCommand = ReactiveCommand.Create(() => screen.Router.NavigateAndReset.Execute(new SettingsPageViewModel(screen)));

			ErrorsChanged += OnErrorsChanged;
		}

		public bool IsPasswordConfirmed
		{
			get => _isPasswordConfirmed;
			set => this.RaiseAndSetIfChanged(ref _isPasswordConfirmed, value);
		}

		public string ErrorMessage
		{
			get => _errorMessage;
			set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
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

		public ICommand GoBackCommand => HostScreen.Router.NavigateBack;
		public ICommand ContinueCommand { get; }
		public ICommand CancelCommand { get; }

		public string? UrlPathSegment => throw new NotImplementedException();
		public IScreen HostScreen { get; }

		private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
		{
			if (Validations is Validations validations)
			{
				var errors = (List<ErrorDescriptor>)validations.GetErrors(e.PropertyName);

				if (errors.Any())
				{
					ErrorMessage = errors.First().Message;
				}
				else
				{
					ErrorMessage = null!;
				}
			}
		}

		private void ValidatePassword(IValidationErrors errors)
		{
			IsPasswordConfirmed = false;
			string password = Password;

			if (PasswordHelper.IsTrimable(password, out _))
			{
				errors.Add(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
				return;
			}

			if (PasswordHelper.IsTooLong(password, out _))
			{
				errors.Add(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage);
				return;
			}

			if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword)
			{
				errors.Add(ErrorSeverity.Error, "Passwords don’t match, check any spelling miskates and try again.");
				return;
			}

			if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword))
			{
				IsPasswordConfirmed = true;

				if (Validations is Validations validations)
				{
					validations.ClearErrors(nameof(Password));
					validations.ClearErrors(nameof(ConfirmPassword));
				}
			}
		}
	}
}
