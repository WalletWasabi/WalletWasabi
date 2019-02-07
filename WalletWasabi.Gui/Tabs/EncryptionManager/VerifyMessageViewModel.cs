using Avalonia.Diagnostics.ViewModels;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Tabs.EncryptionManager;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class VerifyMessageViewModel : CategoryViewModel, IDisposable
	{
		private string _message;
		private string _address;
		private string _signature;
		private string _warningMessage;
		private bool _isVerified;
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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

		public bool IsVerified
		{
			get => _isVerified;
			set => this.RaiseAndSetIfChanged(ref _isVerified, value);
		}

		public ReactiveCommand SignCommand { get; }
		public ReactiveCommand VerifyCommand { get; }
		public EncryptionManagerViewModel Owner { get; }

		public VerifyMessageViewModel(EncryptionManagerViewModel owner) : base("Verify Message")
		{
			Owner = owner;

			this.WhenAnyValue(x => x.Message, x => x.Address, x => x.Signature).Subscribe(_ => IsVerified = false);

			var canVerify = this.WhenAnyValue(x => x.Message, x => x.Address, x => x.Signature,
				(message, address, sign) =>
					!string.IsNullOrEmpty(message) &&
					!string.IsNullOrEmpty(address) &&
					!string.IsNullOrEmpty(sign));

			VerifyCommand = ReactiveCommand.Create(
				() =>
				{
					WarningMessage = "";
					IsVerified = false;
					var verified = VerifyMessage(Address, Message, Signature);
					if (!verified) throw new InvalidOperationException("Message authentication failed!");
					IsVerified = true;
				}
				, canVerify
			);
			VerifyCommand.ThrownExceptions.Subscribe(ex =>
			{
				WarningMessage = ex.Message;
				Dispatcher.UIThread.Post(async () =>
				{
					try
					{
						await Task.Delay(7000, _cancellationTokenSource.Token);
						WarningMessage = "";
					}
					catch (Exception) { };
				});
			});
		}

		private static bool VerifyMessage(string address, string message, string signature)
		{
			BitcoinWitPubKeyAddress addr = new BitcoinWitPubKeyAddress(address, Global.Network);
			return addr.VerifyMessage(message, signature);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false;

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_cancellationTokenSource.Cancel();
					_cancellationTokenSource.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
