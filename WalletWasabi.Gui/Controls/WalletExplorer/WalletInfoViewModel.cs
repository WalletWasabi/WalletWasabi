using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletInfoViewModel : WalletActionViewModel, IDisposable
	{
		private string _password;
		private string _secret;
		private string _warningMessage;
		private CompositeDisposable Disposables { get; }

		public WalletInfoViewModel(WalletViewModel walletViewModel) : base(walletViewModel.Name, walletViewModel)
		{
			Disposables = new CompositeDisposable();
			_password = "";
			_secret = "";
			_warningMessage = "";
			Closing = new CancellationTokenSource().DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				if (x.NotNullAndNotEmpty())
				{
					char lastChar = x.Last();
					if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
					{
						Password = x.TrimEnd('\r', '\n');
					}
				}
			}).DisposeWith(Disposables);

			ShowMasterKeyCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					Password = Guard.Correct(Password);
					var secret = KeyManager.EncryptedSecret.GetSecret(Password);
					Password = "";

					SetSecret(secret.ToWif());
				}
				catch (Exception ex)
				{
					SetWarningMessage(ex.ToTypeMessageString());
				}
			}).DisposeWith(Disposables);
		}

		public CancellationTokenSource Closing { get; }

		public WalletService WalletService => Wallet.WalletService;
		public KeyManager KeyManager => WalletService.KeyManager;

		public string ExtPubKey => KeyManager.ExtPubKey.ToString(Global.Network);
		public string EncryptedSecret => KeyManager.EncryptedSecret.ToWif();
		public string AccountKeyPath => $"m/{KeyManager.AccountKeyPath.ToString()}";

		public ReactiveCommand ShowMasterKeyCommand { get; }

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string Secret
		{
			get => _secret;
			set => this.RaiseAndSetIfChanged(ref _secret, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		private void SetSecret(string secret)
		{
			Secret = secret;

			Dispatcher.UIThread.PostLogException(async () =>
			{
				try
				{
					await Task.Delay(21000, Closing.Token);
				}
				catch (TaskCanceledException)
				{
					// Ignore
				}
				finally
				{
					Secret = "";
				}
			});
		}

		public override void OnDeselected()
		{
			Password = "";
			Secret = "";
			base.OnDeselected();
		}

		public override void OnSelected()
		{
			Password = "";
			Secret = "";
			base.OnSelected();
		}

		public override void Close()
		{
			Password = "";
			Secret = "";
			base.Close();
		}

		private void SetWarningMessage(string message)
		{
			WarningMessage = message;

			Dispatcher.UIThread.PostLogException(async () =>
			{
				try
				{
					await Task.Delay(7000, Closing.Token);
				}
				catch (TaskCanceledException)
				{
					// Ignore
				}
				finally
				{
					if (WarningMessage == message)
					{
						WarningMessage = "";
					}
				}
			});
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Closing?.Cancel();
					Password = "";
					Secret = "";
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
