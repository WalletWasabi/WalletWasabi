using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletInfoViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private bool _showSensitiveKeys;
		private string _password;
		private string _extendedMasterPrivateKey;
		private string _extendedMasterZprv;
		private string _extendedAccountPrivateKey;
		private string _extendedAccountZprv;

		public WalletInfoViewModel(WalletViewModel walletViewModel) : base(walletViewModel.Name, walletViewModel)
		{
			ClearSensitiveData(true);
			SetWarningMessage("");

			ToggleSensitiveKeysCommand = ReactiveCommand.Create(() =>
				{
					try
					{
						if (ShowSensitiveKeys)
						{
							ClearSensitiveData(true);
						}
						else
						{
							var secret = PasswordHelper.GetMasterExtKey(KeyManager, Password, out string isCompatibilityPasswordUsed);
							Password = "";

							if (isCompatibilityPasswordUsed != null)
							{
								SetWarningMessage(PasswordHelper.CompatibilityPasswordWarnMessage);
							}

							string master = secret.GetWif(Global.Network).ToWif();
							string account = secret.Derive(KeyManager.AccountKeyPath).GetWif(Global.Network).ToWif();
							string masterZ = secret.ToZPrv(Global.Network);
							string accountZ = secret.Derive(KeyManager.AccountKeyPath).ToZPrv(Global.Network);
							SetSensitiveData(master, account, masterZ, accountZ);
						}
					}
					catch (Exception ex)
					{
						SetWarningMessage(ex.ToTypeMessageString());
					}
				});
		}

		private void ClearSensitiveData(bool passwordToo)
		{
			ExtendedMasterPrivateKey = "";
			ExtendedMasterZprv = "";
			ExtendedAccountPrivateKey = "";
			ExtendedAccountZprv = "";
			ShowSensitiveKeys = false;

			if (passwordToo)
			{
				Password = "";
			}
		}

		public CancellationTokenSource Closing { private set; get; }

		public string ExtendedAccountPublicKey => KeyManager.ExtPubKey.ToString(Global.Network);
		public string ExtendedAccountZpub => KeyManager.ExtPubKey.ToZpub(Global.Network);
		public string AccountKeyPath => $"m/{KeyManager.AccountKeyPath}";
		public string MasterKeyFingerprint => KeyManager.MasterFingerprint.ToString();
		public ReactiveCommand<Unit, Unit> ToggleSensitiveKeysCommand { get; }

		public bool ShowSensitiveKeys
		{
			get => _showSensitiveKeys;
			set => this.RaiseAndSetIfChanged(ref _showSensitiveKeys, value);
		}

		public ErrorDescriptors ValidatePassword() => PasswordHelper.ValidatePassword(Password);

		[ValidateMethod(nameof(ValidatePassword))]
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

		public string ExtendedAccountPrivateKey
		{
			get => _extendedAccountPrivateKey;
			set => this.RaiseAndSetIfChanged(ref _extendedAccountPrivateKey, value);
		}

		public string ExtendedMasterZprv
		{
			get => _extendedMasterZprv;
			set => this.RaiseAndSetIfChanged(ref _extendedMasterZprv, value);
		}

		public string ExtendedAccountZprv
		{
			get => _extendedAccountZprv;
			set => this.RaiseAndSetIfChanged(ref _extendedAccountZprv, value);
		}

		private void SetSensitiveData(string extendedMasterPrivateKey, string extendedAccountPrivateKey, string extendedMasterZprv, string extendedAccountZprv)
		{
			ExtendedMasterPrivateKey = extendedMasterPrivateKey;
			ExtendedAccountPrivateKey = extendedAccountPrivateKey;
			ExtendedMasterZprv = extendedMasterZprv;
			ExtendedAccountZprv = extendedAccountZprv;
			ShowSensitiveKeys = true;

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

		public override void OnOpen()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Closing = new CancellationTokenSource();

			Global.UiConfig.WhenAnyValue(x => x.ShieldedScreenMode).Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(ExtendedAccountPublicKey));
					this.RaisePropertyChanged(nameof(ExtendedAccountZpub));
				}).DisposeWith(Disposables);

			Closing.DisposeWith(Disposables);

			base.OnOpen();
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

		public override bool OnClose()
		{
			Closing.Cancel();
			Disposables?.Dispose();
			Disposables = null;

			ClearSensitiveData(true);
			return base.OnClose();
		}
	}
}
