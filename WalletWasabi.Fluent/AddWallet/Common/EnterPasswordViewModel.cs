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

		public EnterPasswordViewModel(IScreen screen, Global global, string walletName)
		{
			HostScreen = screen;

			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.ConfirmPassword, ValidateConfirmPassword);

			ContinueCommand = ReactiveCommand.Create(() =>
			{
				var walletGenerator = new WalletGenerator(global.WalletManager.WalletDirectories.WalletsDir, global.Network);
				walletGenerator.TipHeight = global.BitcoinStore.SmartHeaderChain.TipHeight;
				var (km, mnemonic) = walletGenerator.GenerateWallet(walletName, Password);
				screen.Router.Navigate.Execute(new RecoveryWordsViewModel(screen, km, mnemonic, global));
			},
				// Can Execute
				this.WhenAnyValue(x => x.Password, x => x.ConfirmPassword, (password, confirmPassword) =>
				!string.IsNullOrEmpty(password) &&
				!string.IsNullOrEmpty(confirmPassword) &&
				!Validations.Any)
				.ObserveOn(RxApp.MainThreadScheduler)
			);

			this.WhenAnyValue(x => x.Password)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(ConfirmPassword)));

			this.WhenAnyValue(x => x.ConfirmPassword)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Password)));

			ErrorsChanged += OnErrorsChanged;
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
				var errors = new List<ErrorDescriptor>();

				errors.AddRange((List<ErrorDescriptor>)validations.GetErrors(nameof(Password)));
				errors.AddRange((List<ErrorDescriptor>)validations.GetErrors(nameof(ConfirmPassword)));

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
