using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager.LoadWallets
{
	public class LoadWalletViewModel : CategoryViewModel
	{
		private ObservableCollection<LoadWalletEntry> _wallets;
		private string _password;
		private LoadWalletEntry _selectedWallet;
		private bool _isWalletSelected;
		private bool _isWalletOpened;
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
			Wallets = new ObservableCollection<LoadWalletEntry>();

			this.WhenAnyValue(x => x.SelectedWallet)
				.Subscribe(_ => TrySetWalletStates());

			this.WhenAnyValue(x => x.IsWalletOpened)
				.Subscribe(_ => TrySetWalletStates());

			this.WhenAnyValue(x => x.IsBusy)
				.Subscribe(_ => TrySetWalletStates());

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

		public ObservableCollection<LoadWalletEntry> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		[ValidateMethod(nameof(ValidatePassword))]
		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public LoadWalletEntry SelectedWallet
		{
			get => _selectedWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedWallet, value);
		}

		public bool IsWalletSelected
		{
			get => _isWalletSelected;
			set => this.RaiseAndSetIfChanged(ref _isWalletSelected, value);
		}

		public bool IsWalletOpened
		{
			get => _isWalletOpened;
			set => this.RaiseAndSetIfChanged(ref _isWalletOpened, value);
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
			Wallets.Clear();
			Password = "";

			foreach (var file in Global.WalletManager.WalletDirectories.EnumerateWalletFiles())
			{
				var wallet = new LoadWalletEntry(Path.GetFileNameWithoutExtension(file.FullName));
				if (IsPasswordRequired)
				{
					if (KeyManager.TryGetEncryptedSecretFromFile(file.FullName, out _))
					{
						Wallets.Add(wallet);
					}
				}
				else
				{
					Wallets.Add(wallet);
				}
			}

			TrySetWalletStates();

			if (!CanLoadWallet && Wallets.Count > 0)
			{
				NotificationHelpers.Warning("There is already an open wallet. Restart the application in order to open a different one.");
			}
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

				KeyManager keyManager;
				try
				{
					keyManager = Global.LoadKeyManager(walletName);
				}
				catch (FileNotFoundException)
				{
					// The selected wallet is not available any more (someone deleted it?).
					OnCategorySelected();
					NotificationHelpers.Warning("The selected wallet and its backup do not exist, did you delete them?");
					return null;
				}

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
						Owner.SelectTestPassword();
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

					var wallet = await Task.Run(async () => await Global.WalletManager.CreateAndStartWalletAsync(keyManager));
					// Successfully initialized.
					Owner.OnClose();
					// Open Wallet Explorer tabs
					if (wallet.Coins.Any())
					{
						// If already have coins then open the last active tab first.
						IoC.Get<WalletExplorerViewModel>().OpenWallet(wallet, receiveDominant: false);
					}
					else // Else open with Receive tab first.
					{
						IoC.Get<WalletExplorerViewModel>().OpenWallet(wallet, receiveDominant: true);
					}
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

				if (Global.WalletManager.AnyWallet())
				{
					IsWalletOpened = true;
					CanLoadWallet = false;
				}
				else
				{
					IsWalletOpened = false;

					// If not busy loading.
					// And wallet is selected.
					// And no wallet is opened.
					CanLoadWallet = !IsBusy && IsWalletSelected;
				}

				SetLoadButtonText();
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}

			return false;
		}
	}
}
