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
	internal class VerifyMessageViewModel : CategoryViewModel
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

		public VerifyMessageViewModel(EncryptionManagerViewModel owner) : base("Verify Message")
		{
			Owner = owner;

			var canVerify = this.WhenAnyValue(x => x.Message, x => x.Address, x => x.Signature,
				(message, address, sign) =>
					!string.IsNullOrEmpty(message) &&
					!string.IsNullOrEmpty(address) &&
					!string.IsNullOrEmpty(sign));

			VerifyCommand = ReactiveCommand.Create(
				() =>
				{
					WarningMessage = "";
					var verified = VerifyMessage(Address, Message, Signature);
					if (!verified) throw new InvalidOperationException("Invalid signature");
				}
				, canVerify
			);
			VerifyCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
			});
		}

		private static bool VerifyMessage(string address, string message, string signature)
		{
			BitcoinWitPubKeyAddress addr = new BitcoinWitPubKeyAddress(address, Global.Network);
			return addr.VerifyMessage(message, signature);
		}
	}
}
