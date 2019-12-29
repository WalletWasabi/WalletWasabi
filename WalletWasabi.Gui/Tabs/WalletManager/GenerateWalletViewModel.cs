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
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _walletName;
		private bool _termsAccepted;
		public WalletManagerViewModel Owner { get; }
		public Global Global => Owner.Global;

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
		{
			Owner = owner;

			IObservable<bool> canGenerate = this.WhenAnyValue(x => x.Password).Select(pw => !ValidatePassword().HasErrors);

			NextCommand = ReactiveCommand.Create(DoNextCommand, canGenerate);

			NextCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void DoNextCommand()
		{
			WalletName = Guard.Correct(WalletName);

			if (!ValidateWalletName(WalletName))
			{
				NotificationHelpers.Error("Invalid wallet name.");
				return;
			}

			string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");
			if (string.IsNullOrWhiteSpace(WalletName))
			{
				NotificationHelpers.Error("Invalid wallet name.");
			}
			else if (File.Exists(walletFilePath))
			{
				NotificationHelpers.Error("Wallet name is already taken.");
			}
			else
			{
				try
				{
					PasswordHelper.Guard(Password); // Here we are not letting anything that will be autocorrected later. We need to generate the wallet exactly with the entered password bacause of compatibility.

					var km = KeyManager.CreateNew(out Mnemonic mnemonic, Password);
					km.SetNetwork(Global.Network);
					km.SetBestHeight(new Height(Global.BitcoinStore.SmartHeaderChain.TipHeight));
					km.SetFilePath(walletFilePath);
					Owner.CurrentView = new GenerateWalletSuccessViewModel(Owner, km, mnemonic);
				}
				catch (Exception ex)
				{
					NotificationHelpers.Error(ex.ToTypeMessageString());
					Logger.LogError(ex);
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

		public ReactiveCommand<Unit, Unit> NextCommand { get; }

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = "";
			WalletName = Global.GetNextWalletName();
		}
	}
}
