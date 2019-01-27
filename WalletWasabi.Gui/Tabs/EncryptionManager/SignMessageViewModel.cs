using Avalonia.Diagnostics.ViewModels;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Gui.Tabs.EncryptionManager;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class SignMessageViewModel : CategoryViewModel
	{
		private string _message;
		private string _address;
		private string _password;
		private string _signature;
		private string _warningMessage;

		public string Message
		{
			get => _message;
			set => this.RaiseAndSetIfChanged(ref _message, value);
		}

		public string Address
		{
			get => _address;
			set => this.RaiseAndSetIfChanged(ref _address, value);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string Signature
		{
			get => _signature;
			set => this.RaiseAndSetIfChanged(ref _signature, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public ReactiveCommand SignCommand { get; }
		public ReactiveCommand VerifyCommand { get; }
		public EncryptionManagerViewModel Owner { get; }

		public SignMessageViewModel(EncryptionManagerViewModel owner) : base("Sign Message")
		{
			Owner = owner;

			var canSign = this.WhenAnyValue(x => x.Message, x => x.Address,
				(message, address) =>
					!string.IsNullOrEmpty(message) &&
					!string.IsNullOrEmpty(address));

			var canVerify = this.WhenAnyValue(x => x.Message, x => x.Address, x => x.Signature,
				(message, address, sign) =>
					!string.IsNullOrEmpty(message) &&
					!string.IsNullOrEmpty(address) &&
					!string.IsNullOrEmpty(sign));

			SignCommand = ReactiveCommand.Create(
				() =>
				{
					Signature = SignMessage(Address, Message, Password);
				},
				canSign
			);
			SignCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});

			VerifyCommand = ReactiveCommand.Create(
				() =>
				{
					var verified = VerifyMessage(Address, Message, Signature);
					if (!verified) throw new InvalidOperationException("Invalid signature");
					WarningMessage = "Good";
				}
				, canVerify
			);
			VerifyCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});
		}

		private static string SignMessage(string address, string message, string password)
		{
			if (!File.Exists(Global.WalletsDir))
			{
				throw new InvalidOperationException("Wallet directory missing");
			}

			var directoryInfo = new DirectoryInfo(Global.WalletsDir);
			var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
			List<KeyManager> kms = new List<KeyManager>();
			foreach (var file in walletFiles)
			{
				kms.Add(KeyManager.FromFile(file.FullName));
			}
			password = Guard.Correct(password);

			//var tt=kms.Select(password, BitcoinAddress.Create(address, Global.Network))

			//kms.Select( km => km.GetSecrets(password, BitcoinAddress.Create(address, Global.Network)).FirstOrDefault();

			////https://programmingblockchain.gitbook.io/programmingblockchain/bitcoin_transfer/proof_of_ownership_as_an_authentication_method

			//ExtKey result = Global.WalletService.KeyManager.FirstOrDefault();
			//if (result is null)
			//{
			//	throw new InvalidOperationException("Address not found.");
			//}
			//string signature = result.PrivateKey.SignMessage(message);
			return null;
		}

		private static bool VerifyMessage(string address, string message, string signature)
		{
			BitcoinPubKeyAddress addr = new BitcoinPubKeyAddress(address, Global.Network);//TODO: only works for addresses beginning with 1
			return addr.VerifyMessage(message, signature);
		}
	}
}
