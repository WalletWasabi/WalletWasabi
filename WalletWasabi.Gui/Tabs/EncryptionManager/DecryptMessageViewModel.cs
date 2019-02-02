using Avalonia.Diagnostics.ViewModels;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Tabs.EncryptionManager;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class DecryptMessageViewModel : CategoryViewModel
	{
		private string _encryptedMessage;
		private string _password;
		private string _decryptedMessage;
		private string _myPublicKey;
		private string _warningMessage;
		private bool _isPublicKeyPresent;

		public string EncryptedMessage
		{
			get => _encryptedMessage;
			set => this.RaiseAndSetIfChanged(ref _encryptedMessage, value);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string MyPublicKey
		{
			get => _myPublicKey;
			set => this.RaiseAndSetIfChanged(ref _myPublicKey, value);
		}

		public string DecryptedMessage
		{
			get => _decryptedMessage;
			set => this.RaiseAndSetIfChanged(ref _decryptedMessage, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public bool IsPublicKeyPresent
		{
			get => _isPublicKeyPresent;
			set => this.RaiseAndSetIfChanged(ref _isPublicKeyPresent, value);
		}

		public ReactiveCommand DecryptCommand { get; }
		public ReactiveCommand MyPublicKeyCommand { get; }

		public EncryptionManagerViewModel Owner { get; }

		public DecryptMessageViewModel(EncryptionManagerViewModel owner) : base("Decrypt Message")
		{
			Owner = owner;

			var canDecrypt = this.WhenAnyValue(x => x.EncryptedMessage,
				(message) =>
					!string.IsNullOrEmpty(message));

			DecryptCommand = ReactiveCommand.Create(
				() =>
				{
					WarningMessage = "";
					DecryptedMessage = DecryptMessage(EncryptedMessage, Password);
				},
				canDecrypt
			);

			DecryptCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});

			MyPublicKeyCommand = ReactiveCommand.Create(
				() =>
				{
					WarningMessage = "";
					MyPublicKey = Global.WalletService.KeyManager.EncryptedSecret.GetSecret(Password).PubKey.ToHex();
					IsPublicKeyPresent = true;
				}
			);

			MyPublicKeyCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});
		}

		private static string DecryptMessage(string message, string password)
		{
			password = Guard.Correct(password);
			var bitcoinPrivateKey = Global.WalletService.KeyManager.EncryptedSecret.GetSecret(password);
			return bitcoinPrivateKey.PrivateKey.Decrypt(message);
		}
	}
}
