using Avalonia.Diagnostics.ViewModels;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
	internal class SignMessageViewModel : CategoryViewModel, IDisposable
	{
		private string _message;
		private string _address;
		private string _password;
		private string _signature;
		private string _warningMessage;
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
		public EncryptionManagerViewModel Owner { get; }

		public SignMessageViewModel(EncryptionManagerViewModel owner) : base("Sign Message")
		{
			Owner = owner;

			var canSign = this.WhenAnyValue(x => x.Message, x => x.Address,
				(message, address) =>
					!string.IsNullOrEmpty(message) &&
					!string.IsNullOrEmpty(address));

			SignCommand = ReactiveCommand.Create(
				() =>
				{
					Signature = "";
					Signature = SignMessage(Address, Message, Password);
				},
				canSign
			);
			SignCommand.ThrownExceptions.Subscribe(ex =>
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

		private static string SignMessage(string address, string message, string password)
		{
			password = Guard.Correct(password);

			var addr = BitcoinAddress.Create(address, Global.Network);

			var sec = Global.WalletService.KeyManager.GetSecrets(password, addr.ScriptPubKey).FirstOrDefault();

			return sec.PrivateKey.SignMessage(message);
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
