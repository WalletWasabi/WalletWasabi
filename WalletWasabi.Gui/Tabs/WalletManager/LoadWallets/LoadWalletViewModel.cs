using Avalonia;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Tabs.WalletManager.LoadWallets
{
	public class LoadWalletViewModel : CategoryViewModel, IDisposable
	{
		private ReadOnlyObservableCollection<WalletViewModelBase> _wallets;
		private string _password;
		private WalletViewModelBase _selectedWallet;
		private bool _isWalletSelected;
		private bool _canLoadWallet;
		private bool _canTestPassword;
		private bool _isBusy;
		private string _loadButtonText;

		public LoadWalletViewModel(WalletManagerViewModel owner, LoadWalletType loadWalletType)
			: base(loadWalletType == LoadWalletType.Password ? "Test Password" : "Load Wallet")
		{
			Global = Locator.Current.GetService<Global>();

			Owner = owner;
			Password = "";
			LoadWalletType = loadWalletType;

			RootList = new SourceList<WalletViewModelBase>();
			RootList
				.Connect()
				.Filter(x => !IsPasswordRequired || !x.Wallet.KeyManager.IsWatchOnly)
				.Sort(SortExpressionComparer<WalletViewModelBase>.Descending(p => p.Wallet.KeyManager.GetLastAccessTime()))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _wallets)
				.DisposeMany()
				.Subscribe();

			this.WhenAnyValue(x => x.SelectedWallet)
				.Subscribe(_ => TrySetWalletStates());

			this.WhenAnyValue(x => x.IsBusy)
				.Subscribe(_ => TrySetWalletStates());

			Observable.FromEventPattern<Wallet>(Global.WalletManager, nameof(Global.WalletManager.WalletAdded))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => x.EventArgs)
				.Subscribe(wallet => RootList.Add(new WalletViewModelBase(wallet)));

			UpdateWalletList();

			LoadCommand = ReactiveCommand.CreateFromTask(LoadWalletAsync, this.WhenAnyValue(x => x.CanLoadWallet));
			TestPasswordCommand = ReactiveCommand.Create(LoadKeyManager, this.WhenAnyValue(x => x.CanTestPassword));
			OpenFolderCommand = ReactiveCommand.Create(OpenWalletsFolder);

			Observable
				.Merge(LoadCommand.ThrownExceptions)
				.Merge(TestPasswordCommand.ThrownExceptions)
				.Merge(OpenFolderCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});

			SetLoadButtonText();
		}

		public LoadWalletType LoadWalletType { get; }
		public bool IsPasswordRequired => LoadWalletType == LoadWalletType.Password;
		public bool IsDesktopWallet => LoadWalletType == LoadWalletType.Desktop;

		public ReadOnlyObservableCollection<WalletViewModelBase> Wallets => _wallets;

		[ValidateMethod(nameof(ValidatePassword))]
		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public WalletViewModelBase SelectedWallet
		{
			get => _selectedWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedWallet, value);
		}

		public bool IsWalletSelected
		{
			get => _isWalletSelected;
			set => this.RaiseAndSetIfChanged(ref _isWalletSelected, value);
		}

		public string LoadButtonText
		{
			get => _loadButtonText;
			set => this.RaiseAndSetIfChanged(ref _loadButtonText, value);
		}

		public bool CanLoadWallet
		{
			get => _canLoadWallet;
			set => this.RaiseAndSetIfChanged(ref _canLoadWallet, value);
		}

		public bool CanTestPassword
		{
			get => _canTestPassword;
			set => this.RaiseAndSetIfChanged(ref _canTestPassword, value);
		}

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public SourceList<WalletViewModelBase> RootList { get; private set; }
		public ReactiveCommand<Unit, Unit> LoadCommand { get; }
		public ReactiveCommand<Unit, KeyManager> TestPasswordCommand { get; }
		public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
		private WalletManagerViewModel Owner { get; }

		private Global Global { get; }

		public ErrorDescriptors ValidatePassword() => PasswordHelper.ValidatePassword(Password);

		public void SetLoadButtonText()
		{
			var text = "Load Wallet";
			if (IsBusy)
			{
				text = "Loading...";
			}

			LoadButtonText = text;
		}

		public override void OnCategorySelected()
		{
			Password = "";

			TrySetWalletStates();
		}

		public KeyManager LoadKeyManager()
		{
			try
			{
				CanTestPassword = false;
				var password = Guard.Correct(Password); // Do not let whitespaces to the beginning and to the end.
				Password = ""; // Clear password field.

				var selectedWallet = SelectedWallet;
				if (selectedWallet is null)
				{
					NotificationHelpers.Warning("No wallet selected.");
					return null;
				}

				var walletName = selectedWallet.WalletName;

				KeyManager keyManager = Global.WalletManager.GetWalletByName(walletName).KeyManager;

				// Only check requirepassword here, because the above checks are applicable to loadwallet, too and we are using this function from load wallet.
				if (IsPasswordRequired)
				{
					if (PasswordHelper.TryPassword(keyManager, password, out string compatibilityPasswordUsed))
					{
						NotificationHelpers.Success("Correct password.");
						if (compatibilityPasswordUsed != null)
						{
							NotificationHelpers.Warning(PasswordHelper.CompatibilityPasswordWarnMessage);
						}

						keyManager.SetPasswordVerified();
					}
					else
					{
						NotificationHelpers.Error("Wrong password.");
						return null;
					}
				}
				else
				{
					if (keyManager.PasswordVerified == false)
					{
						Owner.SelectTestPassword(walletName);
						return null;
					}
				}

				return keyManager;
			}
			catch (Exception ex)
			{
				// Initialization failed.
				NotificationHelpers.Error(ex.ToUserFriendlyString());
				Logger.LogError(ex);

				return null;
			}
			finally
			{
				CanTestPassword = IsWalletSelected;
			}
		}

		public async Task LoadWalletAsync()
		{
			try
			{
				IsBusy = true;

				var keyManager = LoadKeyManager();
				if (keyManager is null)
				{
					return;
				}

				try
				{
					bool isSuccessful = await Global.WaitForInitializationCompletedAsync(CancellationToken.None);
					if (!isSuccessful)
					{
						return;
					}

					var firstWalletToLoad = !Global.WalletManager.AnyWallet();

					var wallet = await Task.Run(async () => await Global.WalletManager.StartWalletAsync(keyManager));
					// Successfully initialized.
					if (firstWalletToLoad)
					{
						Owner.OnClose();
					}

					UpdateWalletList();
				}
				catch (Exception ex)
				{
					// Initialization failed.
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					if (!(ex is OperationCanceledException))
					{
						Logger.LogError(ex);
					}
				}
			}
			finally
			{
				IsBusy = false;
			}
		}

		public void OpenWalletsFolder() => IoHelpers.OpenFolderInFileExplorer(Global.WalletManager.WalletDirectories.WalletsDir);

		private void UpdateWalletList()
		{
			RootList.Clear();
			RootList.AddRange(Global.WalletManager
				.GetWallets()
				.Where(x => x.State == WalletState.Uninitialized)
				.Select(x => new WalletViewModelBase(x)));
		}

		private bool TrySetWalletStates()
		{
			try
			{
				if (SelectedWallet is null)
				{
					SelectedWallet = Wallets.FirstOrDefault();
				}

				IsWalletSelected = SelectedWallet != null;
				CanTestPassword = IsWalletSelected;
				CanLoadWallet = SelectedWallet is { } ? (!SelectedWallet.IsBusy && SelectedWallet.WalletState <= WalletState.Initialized) : false;

				SetLoadButtonText();
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}

			return false;
		}

		#region IDisposable Support

		private bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					RootList.Dispose();
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
