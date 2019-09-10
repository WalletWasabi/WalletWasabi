using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _walletName;
		private bool _termsAccepted;
		private string _validationMessage;
		public WalletManagerViewModel Owner { get; }
		public Global Global => Owner.Global;

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
		{
			Owner = owner;

			IObservable<bool> canGenerate = Observable.CombineLatest(
				this.WhenAnyValue(x => x.TermsAccepted),
				this.WhenAnyValue(x => x.Password).Select(pw => !ValidatePassword().HasErrors),
				(terms, pw) => terms && pw);

			GenerateCommand = ReactiveCommand.Create(DoGenerateCommand, canGenerate);
		}

		private void DoGenerateCommand()
		{
			WalletName = Guard.Correct(WalletName);

			if (!ValidateWalletName(WalletName))
			{
				ValidationMessage = $"The name {WalletName} is not valid.";
				return;
			}

			string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");

			if (!TermsAccepted)
			{
				ValidationMessage = "Terms are not accepted.";
			}
			else if (string.IsNullOrWhiteSpace(WalletName))
			{
				ValidationMessage = $"The name {WalletName} is not valid.";
			}
			else if (File.Exists(walletFilePath))
			{
				ValidationMessage = $"The name {WalletName} is already taken.";
			}
			else
			{
				try
				{
					PasswordHelper.Guard(Password); // Here we are not letting anything that will be autocorrected later. We need to generate the wallet exactly with the entered password bacause of compatibility.

					KeyManager.CreateNew(out Mnemonic mnemonic, Password, walletFilePath);

					Owner.CurrentView = new GenerateWalletSuccessViewModel(Owner, mnemonic);
				}
				catch (Exception ex)
				{
					ValidationMessage = ex.ToTypeMessageString();
					Logger.LogError<GenerateWalletViewModel>(ex);
				}
			}
		}

		private static readonly string[] ReservedFileNames = new string[]
		{
			"CON", "PRN", "AUX", "NUL",
			"COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
			"LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
		};

		private bool ValidateWalletName(string walletName)
		{
			var invalidChars = Path.GetInvalidFileNameChars();
			var isValid = !walletName.Any(c => invalidChars.Contains(c)) && !walletName.EndsWith(".");
			var isReserved = ReservedFileNames.Any(w => walletName.ToUpper() == w || walletName.ToUpper().StartsWith(w + "."));
			return isValid && !isReserved;
		}

		public ErrorDescriptors ValidatePassword()
		{
			string password = Password;

			var errors = new ErrorDescriptors();

			if (PasswordHelper.IsTrimable(password, out _))
			{
				errors.Add(new ErrorDescriptor(ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!"));
			}

			if (PasswordHelper.IsTooLong(password, out _))
			{
				errors.Add(new ErrorDescriptor(ErrorSeverity.Error, PasswordHelper.PasswordTooLongMessage));
			}

			return errors;
		}

		[ValidateMethod(nameof(ValidatePassword))]
		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}

		public bool TermsAccepted
		{
			get => _termsAccepted;
			set => this.RaiseAndSetIfChanged(ref _termsAccepted, value);
		}

		public string ValidationMessage
		{
			get => _validationMessage;
			set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
		}

		public ReactiveCommand<Unit, Unit> GenerateCommand { get; }

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel(Global));
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel(Global));
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel(Global));
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = "";
			WalletName = Global.GetNextWalletName();
			TermsAccepted = false;
			ValidationMessage = "";
		}
	}
}
