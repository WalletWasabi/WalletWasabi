using Avalonia.Diagnostics.ViewModels;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
		private ObservableCollection<AddressPubKeyViewModel> _addresses;
		private AddressPubKeyViewModel _selectedItem;

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

		public AddressPubKeyViewModel SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		public ObservableCollection<AddressPubKeyViewModel> Addresses
		{
			get => _addresses;
			set => this.RaiseAndSetIfChanged(ref _addresses, value);
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
					DecryptedMessage = DecryptMessage(EncryptedMessage, MyPublicKey, Password);
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
					DecryptedMessage = DecryptMessage(EncryptedMessage, MyPublicKey, Password);
				},
				canDecrypt
			);

			MyPublicKeyCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});

			this.WhenAnyValue(x => x.SelectedItem)
				.Subscribe((x) => MyPublicKey = x is null ? "" : x.PubKey);

			IEnumerable<HdPubKey> keys = Global.WalletService.KeyManager.GetKeys();

			Addresses = new ObservableCollection<AddressPubKeyViewModel>(keys.Select(a => new AddressPubKeyViewModel(a)));
		}

		private string DecryptMessage(string message, string pubkey, string password)
		{
			password = Guard.Correct(password);
			HdPubKey hdPubKey = Global.WalletService.KeyManager.GetKeys().FirstOrDefault(k => k.PubKey.ToHex() == pubkey);
			if (hdPubKey == null)
			{
				throw new InvalidOperationException("Could not fint the corresponting address in your wallet for that public key.");
			}

			(ExtKey secret, HdPubKey pubKey) secret = Global.WalletService.KeyManager.GetSecretsAndPubKeyPairs(password, hdPubKey.PubKey.ScriptPubKey).FirstOrDefault();
			if (secret.Equals(default))
			{
				throw new InvalidOperationException("Could not fint the corresponting secret in your wallet for that ScriptPubKey");
			}
			return secret.secret.PrivateKey.Decrypt(message);
		}
	}
}
