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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
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

		private bool _disposedValue = false; // To detect redundant calls

		public LoadWalletViewModel(WalletManagerViewModel owner, LoadWalletType loadWalletType)
			: base(loadWalletType == LoadWalletType.Password ? "Test Password" : "Load Wallet")
		{
			Global = Locator.Current.GetService<Global>();

			Owner = owner;
			Password = "";
			LoadWalletType = loadWalletType;

			this.ValidateProperty(x => x.Password, ValidatePassword);

			RootList = new SourceList<WalletViewModelBase>();
			RootList.Connect()
				.AutoRefresh(model => model.WalletState)
				.Filter(x => (!IsPasswordRequired || !x.Wallet.KeyManager.IsWatchOnly))
				.Sort(SortExpressionComparer<WalletViewModelBase>
					.Ascending(p => p.WalletState).ThenByDescending(p => p.Wallet.KeyManager.GetLastAccessTime()),
					resort: ResortTrigger.AsObservable())
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _wallets)
				.DisposeMany()
				.Subscribe()
				.DisposeWith(Disposables);

			Observable.FromEventPattern<Wallet>(Global.WalletManager, nameof(Global.WalletManager.WalletAdded))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(x => x.EventArgs)
				.Subscribe(wallet => RootList.Add(new WalletViewModelBase(wallet)))
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.SelectedWallet)
				.Where(x => x is null)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SelectedWallet = Wallets.FirstOrDefault());

			Wallets
				.ToObservableChangeSet()
				.ToCollection()
				.Where(items => items.Any() && SelectedWallet is null)
				.Select(items => items.First())
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => SelectedWallet = x);

			LoadCommand = ReactiveCommand.Create(() =>
				RxApp.MainThreadScheduler
					.Schedule(async () => await LoadWalletAsync())
					.DisposeWith(Disposables),
				this.WhenAnyValue(x => x.SelectedWallet, x => x?.WalletState)
					.Select(x => x == WalletState.Uninitialized));

			TestPasswordCommand = ReactiveCommand.Create(LoadKeyManager, this.WhenAnyValue(x => x.SelectedWallet).Select(x => x is { }));
			OpenFolderCommand = ReactiveCommand.Create(OpenWalletsFolder);

			RootList.AddRange(Global.WalletManager
				.GetWallets()
				.Select(x => new WalletViewModelBase(x)));

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
		}

		public LoadWalletType LoadWalletType { get; }
		public bool IsPasswordRequired => LoadWalletType == LoadWalletType.Password;
		public bool IsDesktopWallet => LoadWalletType == LoadWalletType.Desktop;

		public ReadOnlyObservableCollection<WalletViewModelBase> Wallets => _wallets;

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

		public SourceList<WalletViewModelBase> RootList { get; private set; }
		public ReactiveCommand<Unit, IDisposable> LoadCommand { get; }
		public ReactiveCommand<Unit, KeyManager> TestPasswordCommand { get; }
		public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
		private WalletManagerViewModel Owner { get; }

		private Global Global { get; }

		private ReplaySubject<Unit> ResortTrigger { get; } = new ReplaySubject<Unit>();
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		private void ValidatePassword(IValidationErrors errors) => PasswordHelper.ValidatePassword(errors, Password);

		public override void OnCategorySelected()
		{
			Password = "";
		}

		public KeyManager LoadKeyManager()
		{
			try
			{
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
		}

		public async Task LoadWalletAsync()
		{
			var keyManager = LoadKeyManager();
			if (keyManager is null)
			{
				return;
			}

			try
			{
				await Task.Run(async () => await Global.WalletManager.StartWalletAsync(keyManager));
				ResortTrigger.OnNext(new Unit());
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				NotificationHelpers.Error($"Couldn't load wallet: {Title}. Reason: {ex.ToUserFriendlyString()}");
				Logger.LogError(ex);
			}
		}

		public void OpenWalletsFolder() => IoHelpers.OpenFolderInFileExplorer(Global.WalletManager.WalletDirectories.WalletsDir);

		public void SelectWallet(string walletName)
		{
			var keyman = Wallets.FirstOrDefault(w => w.Wallet.WalletName == walletName)?.Wallet.KeyManager;
			SelectWallet(keyman);
		}

		public void SelectWallet(KeyManager keymanager)
		{
			var wallet = Wallets.FirstOrDefault(w => w.Wallet.KeyManager == keymanager);
			SelectedWallet = wallet;
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables.Dispose();
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
