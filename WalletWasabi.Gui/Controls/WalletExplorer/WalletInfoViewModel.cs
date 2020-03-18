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
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletInfoViewModel : WasabiDocumentTabViewModel
	{
		private bool _showSensitiveKeys;
		private string _password;
		private string _extendedMasterPrivateKey;
		private string _extendedMasterZprv;
		private string _extendedAccountPrivateKey;
		private string _extendedAccountZprv;

		public WalletInfoViewModel(WalletService walletService) : base(walletService.Name)
		{
			Global = Locator.Current.GetService<Global>();
			WalletService = walletService;

			ClearSensitiveData(true);

			ToggleSensitiveKeysCommand = ReactiveCommand.Create(() =>
				{
					if (ShowSensitiveKeys)
					{
						ClearSensitiveData(true);
					}
					else
					{
						var secret = PasswordHelper.GetMasterExtKey(WalletService.KeyManager, Password, out string isCompatibilityPasswordUsed);
						Password = "";

						if (isCompatibilityPasswordUsed != null)
						{
							NotificationHelpers.Warning(PasswordHelper.CompatibilityPasswordWarnMessage);
						}

						string master = secret.GetWif(Global.Network).ToWif();
						string account = secret.Derive(WalletService.KeyManager.AccountKeyPath).GetWif(Global.Network).ToWif();
						string masterZ = secret.ToZPrv(Global.Network);
						string accountZ = secret.Derive(WalletService.KeyManager.AccountKeyPath).ToZPrv(Global.Network);
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

		private WalletService WalletService { get; }

		public CancellationTokenSource Closing { get; private set; }

		public string ExtendedAccountPublicKey => WalletService.KeyManager.ExtPubKey.ToString(Global.Network);
		public string ExtendedAccountZpub => WalletService.KeyManager.ExtPubKey.ToZpub(Global.Network);
		public string AccountKeyPath => $"m/{WalletService.KeyManager.AccountKeyPath}";
		public string MasterKeyFingerprint => WalletService.KeyManager.MasterFingerprint.ToString();
		public ReactiveCommand<Unit, Unit> ToggleSensitiveKeysCommand { get; }
		public bool IsWatchOnly => WalletService.KeyManager.IsWatchOnly;

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
