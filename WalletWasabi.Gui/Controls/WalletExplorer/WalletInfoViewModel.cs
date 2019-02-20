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
		private string _extendedMasterPrivateKey;
		private string _extendedMasterPublicKey;
		private string _extendedAccountPrivateKey;
		private string _warningMessage;
		private CompositeDisposable Disposables { get; }

		public WalletInfoViewModel(WalletViewModel walletViewModel) : base(walletViewModel.Name, walletViewModel)
		{
			Disposables = new CompositeDisposable();
			ClearSensitiveData(true);
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

			ShowSensitiveKeysCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					Password = Guard.Correct(Password);
					var secret = KeyManager.GetExtKey(Password);
					Password = "";

					string master = secret.GetWif(Global.Network).ToWif();
					string masterPub = secret.Neuter().GetWif(Global.Network).ToWif();
					string account = secret.Derive(KeyManager.AccountKeyPath).GetWif(Global.Network).ToWif();
					SetSensitiveData(master, masterPub, account);
				}
				catch (Exception ex)
				{
					SetWarningMessage(ex.ToTypeMessageString());
				}
			}).DisposeWith(Disposables);
		}

		private void ClearSensitiveData(bool passwordToo)
		{
			ExtendedMasterPrivateKey = "";
			ExtendedMasterPublicKey = "";
			ExtendedAccountPrivateKey = "";

			if (passwordToo)
			{
				Password = "";
			}
		}

		public CancellationTokenSource Closing { get; }

		public WalletService WalletService => Wallet.WalletService;
		public KeyManager KeyManager => WalletService.KeyManager;

		public string ExtendedAccountPublicKey => KeyManager.ExtPubKey.ToWif(Global.Network);
		public string EncryptedExtendedMasterPrivateKey => KeyManager.EncryptedSecret.ToWif();
		public string AccountKeyPath => $"m/{KeyManager.AccountKeyPath.ToString()}";

		public ReactiveCommand ShowSensitiveKeysCommand { get; }

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string ExtendedMasterPrivateKey
		{
			get => _extendedMasterPrivateKey;
			set => this.RaiseAndSetIfChanged(ref _extendedMasterPrivateKey, value);
		}

		public string ExtendedMasterPublicKey
		{
			get => _extendedMasterPublicKey;
			set => this.RaiseAndSetIfChanged(ref _extendedMasterPublicKey, value);
		}

		public string ExtendedAccountPrivateKey
		{
			get => _extendedAccountPrivateKey;
			set => this.RaiseAndSetIfChanged(ref _extendedAccountPrivateKey, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		private void SetSensitiveData(string extendedMasterPrivateKey, string extendedMasterPublicKey, string extendedAccountPrivateKey)
		{
			ExtendedMasterPrivateKey = extendedMasterPrivateKey;
			ExtendedMasterPublicKey = extendedMasterPublicKey;
			ExtendedAccountPrivateKey = extendedAccountPrivateKey;

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
					ClearSensitiveData(false);
				}
			});
		}

		public override void OnDeselected()
		{
			ClearSensitiveData(true);
			base.OnDeselected();
		}

		public override void OnSelected()
		{
			ClearSensitiveData(true);
			base.OnSelected();
		}

		public override void Close()
		{
			ClearSensitiveData(true);
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
					ClearSensitiveData(true);
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
