using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletInfoViewModel : WasabiDocumentTabViewModel, IWalletViewModel
	{
		private const string HiddenKeyString = "Sensitive Data - Not Shown";
		private bool _showSensitiveKeys;
		private string _password;
		private string _extendedMasterPrivateKey;
		private string _extendedMasterZprv;
		private string _extendedAccountPrivateKey;
		private string _extendedAccountZprv;

		public WalletInfoViewModel(Wallet wallet) : base(wallet.WalletName)
		{
			Global = Locator.Current.GetService<Global>();
			Wallet = wallet;

			this.ValidateProperty(x => x.Password, ValidatePassword);

			ClearSensitiveData(true);

			ToggleSensitiveKeysCommand = ReactiveCommand.Create(() =>
				{
					if (ShowSensitiveKeys)
					{
						ClearSensitiveData(true);
					}
					else
					{
						var secret = PasswordHelper.GetMasterExtKey(Wallet.KeyManager, Password, out string isCompatibilityPasswordUsed);
						Password = "";

						if (isCompatibilityPasswordUsed != null)
						{
							NotificationHelpers.Warning(PasswordHelper.CompatibilityPasswordWarnMessage);
						}

						string master = secret.GetWif(Global.Network).ToWif();
						string account = secret.Derive(Wallet.KeyManager.AccountKeyPath).GetWif(Global.Network).ToWif();
						string masterZ = secret.ToZPrv(Global.Network);
						string accountZ = secret.Derive(Wallet.KeyManager.AccountKeyPath).ToZPrv(Global.Network);
						SetSensitiveData(master, account, masterZ, accountZ);
					}
				});

			ToggleSensitiveKeysCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					Logger.LogError(ex);
				});
		}

		private Global Global { get; }

		private Wallet Wallet { get; }

		Wallet IWalletViewModel.Wallet => Wallet;

		public CancellationTokenSource Closing { get; private set; }

		public string ExtendedAccountPublicKey => Wallet.KeyManager.ExtPubKey.ToString(Global.Network);
		public string ExtendedAccountZpub => Wallet.KeyManager.ExtPubKey.ToZpub(Global.Network);
		public string AccountKeyPath => $"m/{Wallet.KeyManager.AccountKeyPath}";
		public string MasterKeyFingerprint => Wallet.KeyManager.MasterFingerprint.ToString();
		public ReactiveCommand<Unit, Unit> ToggleSensitiveKeysCommand { get; }
		public bool IsWatchOnly => Wallet.KeyManager.IsWatchOnly;

		public bool ShowSensitiveKeys
		{
			get => _showSensitiveKeys;
			set => this.RaiseAndSetIfChanged(ref _showSensitiveKeys, value);
		}

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

		public void ValidatePassword(IValidationErrors errors) => PasswordHelper.ValidatePassword(errors, Password);

		private void ClearSensitiveData(bool passwordToo)
		{
			ExtendedMasterPrivateKey = HiddenKeyString;
			ExtendedMasterZprv = HiddenKeyString;
			ExtendedAccountPrivateKey = HiddenKeyString;
			ExtendedAccountZprv = HiddenKeyString;
			ShowSensitiveKeys = false;

			if (passwordToo)
			{
				Password = "";
			}
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

		public override void OnOpen(CompositeDisposable disposables)
		{
			Closing = new CancellationTokenSource();

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(ExtendedAccountPublicKey));
					this.RaisePropertyChanged(nameof(ExtendedAccountZpub));
				}).DisposeWith(disposables);

			Closing.DisposeWith(disposables);

			base.OnOpen(disposables);
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

			ClearSensitiveData(true);
			return base.OnClose();
		}
	}
}
