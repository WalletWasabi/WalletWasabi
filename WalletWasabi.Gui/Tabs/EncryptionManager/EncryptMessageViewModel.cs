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
	internal class EncryptMessageViewModel : CategoryViewModel
	{
		private string _plainMessage;
		private string _password;
		private string _encryptedMessage;
		private string _warningMessage;
		private string _publicKey;

		public string PlainMessage
		{
			get => _plainMessage;
			set => this.RaiseAndSetIfChanged(ref _plainMessage, value);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string PublicKey
		{
			get => _publicKey;
			set => this.RaiseAndSetIfChanged(ref _publicKey, value);
		}

		public string EncryptedMessage
		{
			get => _encryptedMessage;
			set => this.RaiseAndSetIfChanged(ref _encryptedMessage, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public ReactiveCommand EncryptCommand { get; }
		public EncryptionManagerViewModel Owner { get; }

		public EncryptMessageViewModel(EncryptionManagerViewModel owner) : base("Encrypt Message")
		{
			Owner = owner;

			var canEncrypt = this.WhenAnyValue(x => x.PlainMessage,
				(message) =>
					!string.IsNullOrEmpty(message));

			EncryptCommand = ReactiveCommand.Create(
				() =>
				{
					EncryptedMessage = EncryptMessage(PlainMessage, PublicKey);
				},
				canEncrypt
			);

			EncryptCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});
		}

		private static string EncryptMessage(string message, string pubkey)
		{
			var pk = new PubKey(pubkey);
			return pk.Encrypt(message);
		}
	}
}
