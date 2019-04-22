using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class LoadWalletViewModel : CategoryViewModel
	{
		private ObservableCollection<LoadWalletEntry> _wallets;
		private string _password;
		private LoadWalletEntry _selectedWallet;
		private bool _isWalletSelected;
		private bool _isWalletOpened;
		private bool _canLoadWallet;
		private bool _canTestPassword;
		private string _warningMessage;
		private string _validationMessage;
		private string _successMessage;
		private bool _isBusy;
		private string _loadButtonText;
		private bool _isHwWalletSearchTextVisible;

		public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

		private WalletManagerViewModel Owner { get; }
		public LoadWalletType LoadWalletType { get; }

		public bool IsPasswordRequired => LoadWalletType == LoadWalletType.Password;
		public bool IsHardwareWallet => LoadWalletType == LoadWalletType.Hardware;
		public bool IsDesktopWallet => LoadWalletType == LoadWalletType.Desktop;

		private object WalletLock { get; }

		public LoadWalletViewModel(WalletManagerViewModel owner, LoadWalletType loadWalletType) : base(loadWalletType == LoadWalletType.Password ? "Test Password" : (loadWalletType == LoadWalletType.Desktop ? "Load Wallet" : "Hardware Wallet"))
		{
			Owner = owner;
			Password = "";
			LoadWalletType = loadWalletType;
			Wallets = new ObservableCollection<LoadWalletEntry>();
			WalletLock = new object();

			this.WhenAnyValue(x => x.SelectedWallet)
				.Subscribe(selectedWallet => SetWalletStates());

			this.WhenAnyValue(x => x.IsWalletOpened)
				.Subscribe(isWalletOpened => SetWalletStates());

			this.WhenAnyValue(x => x.Password).Subscribe(async x =>
			{
				try
				{
					if (x.NotNullAndNotEmpty())
					{
						char lastChar = x.Last();
						if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
						{
							Password = x.TrimEnd('\r', '\n');
							await LoadKeyManagerAsync(requirePassword: true, isHardwareWallet: false);
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogTrace(ex);
				}
			});

			LoadCommand = ReactiveCommand.CreateFromTask(async () => await LoadWalletAsync(), this.WhenAnyValue(x => x.CanLoadWallet));
			TestPasswordCommand = ReactiveCommand.CreateFromTask(async () => await LoadKeyManagerAsync(requirePassword: true, isHardwareWallet: false), this.WhenAnyValue(x => x.CanTestPassword));
			OpenFolderCommand = ReactiveCommand.Create(OpenWalletsFolder);

			LoadCommand.ThrownExceptions.Subscribe(ex => Logger.LogWarning<LoadWalletViewModel>(ex));
			TestPasswordCommand.ThrownExceptions.Subscribe(ex => Logger.LogWarning<LoadWalletViewModel>(ex));
			OpenFolderCommand.ThrownExceptions.Subscribe(ex => Logger.LogWarning<LoadWalletViewModel>(ex));

			SetLoadButtonText(IsBusy);

			IsHwWalletSearchTextVisible = LoadWalletType == LoadWalletType.Hardware;
		}

		public bool IsHwWalletSearchTextVisible
		{
			get => _isHwWalletSearchTextVisible;
			set => this.RaiseAndSetIfChanged(ref _isHwWalletSearchTextVisible, value);
		}

		public ObservableCollection<LoadWalletEntry> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

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

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public string ValidationMessage
		{
			get => _validationMessage;
			set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
		}

		public string SuccessMessage
		{
			get => _successMessage;
			set => this.RaiseAndSetIfChanged(ref _successMessage, value);
		}

		private void SetWarningMessage(string message)
		{
			WarningMessage = message;
			ValidationMessage = "";
			SuccessMessage = "";
		}

		public void SetValidationMessage(string message)
		{
			WarningMessage = "";
			ValidationMessage = message;
			SuccessMessage = "";
		}

		private void SetSuccessMessage(string message)
		{
			WarningMessage = "";
			ValidationMessage = "";
			SuccessMessage = message;
		}

		public void SetLoadButtonText(bool isBusy)
		{
			LoadButtonText = isBusy ? "Loading..." : "Load Wallet";
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
			set
			{
				this.RaiseAndSetIfChanged(ref _isBusy, value);

				SetLoadButtonText(value);
				SetWalletStates();

				const string loadingStatusText = "Loading...";
				if (value)
				{
					MainWindowViewModel.Instance.StatusBar.AddStatus(loadingStatusText);
				}
				else
				{
					MainWindowViewModel.Instance.StatusBar.RemoveStatus(loadingStatusText);
				}
			}
		}

		public override void OnCategorySelected()
		{
			if (IsHardwareWallet)
			{
				return;
			}

			lock (WalletLock)
			{
				Wallets.Clear();
				Password = "";
				SetValidationMessage("");

				var directoryInfo = new DirectoryInfo(Global.WalletsDir);
				var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
				foreach (var file in walletFiles)
				{
					var wallet = new LoadWalletEntry(Path.GetFileNameWithoutExtension(file.FullName));
					Wallets.Add(wallet);
				}

				SelectedWallet = Wallets.FirstOrDefault();
				SetWalletStates();
			}
		}

		private void SetWalletStates()
		{
			IsWalletSelected = SelectedWallet != null;
			CanTestPassword = IsWalletSelected;

			IsWalletOpened = Global.WalletService != null;
			// If not busy loading.
			// And wallet is selected.
			// And no wallet is opened.
			CanLoadWallet = !IsBusy && IsWalletSelected && !IsWalletOpened;

			if (IsWalletOpened)
			{
				SetWarningMessage("There is already an open wallet. Restart the application in order to open a different one.");
			}
		}

		public ReactiveCommand<Unit, Unit> LoadCommand { get; }
		public ReactiveCommand<Unit, KeyManager> TestPasswordCommand { get; }

		public void TryRefreshHardwareWallets(IEnumerable<HardwareWalletInfo> hwis)
		{
			lock (WalletLock)
			{
				var changed = false;
				foreach (var hwi in hwis)
				{
					if (Wallets.All(x => x.HardwareWalletInfo.Path != hwi.Path)) // If it's not already in the list, then add.
					{
						Wallets.Add(new LoadWalletEntry(hwi));
						changed = true;
					}
				}

				var toRemove = new List<LoadWalletEntry>();
				foreach (var wallet in Wallets)
				{
					if (hwis.All(x => x.Path != wallet.HardwareWalletInfo.Path)) // If it's not in the list anymore, then remove.
					{
						toRemove.Add(wallet);
						changed = true;
					}
				}

				foreach (var wallet in toRemove)
				{
					Wallets.Remove(wallet);
				}

				if (changed)
				{
					SetWalletStates();
				}

				if (hwis.Any())
				{
					IsHwWalletSearchTextVisible = false;
					SelectedWallet = Wallets.FirstOrDefault();
				}
				else
				{
					IsHwWalletSearchTextVisible = true;
				}
			}
		}

		public async Task<KeyManager> LoadKeyManagerAsync(bool requirePassword, bool isHardwareWallet)
		{
			try
			{
				CanTestPassword = false;
				var password = Guard.Correct(Password); // Don't let whitespaces to the beginning and to the end.
				Password = ""; // Clear password field.

				if (SelectedWallet is null)
				{
					SetValidationMessage("No wallet selected.");
					return null;
				}

				var selectedWallet = SelectedWallet;
				var walletName = selectedWallet.WalletName;
				if (isHardwareWallet)
				{
					if (selectedWallet is null)
					{
						SetValidationMessage("No hardware wallets detected.");
						return null;
					}

					if (!TryFindWalletByMasterFingerprint(selectedWallet.HardwareWalletInfo.MasterFingerprint, out walletName))
					{
						ExtPubKey extPubKey = await HwiProcessManager.GetXpubAsync(selectedWallet.HardwareWalletInfo);

						Logger.LogInfo<LoadWalletViewModel>("Hardware wallet wasn't used previously on this computer. Creating new wallet file.");

						walletName = Utils.GetNextHardwareWalletName(selectedWallet.HardwareWalletInfo);
						var path = Global.GetWalletFullPath(walletName);
						KeyManager.CreateNewHardwareWalletWatchOnly(selectedWallet.HardwareWalletInfo.MasterFingerprint, extPubKey, path);
					}
				}

				var walletFullPath = Global.GetWalletFullPath(walletName);
				var walletBackupFullPath = Global.GetWalletBackupFullPath(walletName);
				if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
				{
					// The selected wallet is not available any more (someone deleted it?).
					OnCategorySelected();
					SetValidationMessage("The selected wallet and its backup don't exist, did you delete them?");
					return null;
				}

				KeyManager keyManager = Global.LoadKeyManager(walletFullPath, walletBackupFullPath);
				keyManager.HardwareWalletInfo = selectedWallet.HardwareWalletInfo;

				if (!requirePassword && keyManager.PasswordVerified == false)
				{
					Owner.SelectTestPassword();
					return null;
				}
				// Only check requirepassword here, because the above checks are applicable to loadwallet, too and we are using this function from load wallet.
				if (requirePassword)
				{
					if (!keyManager.TestPassword(password))
					{
						SetValidationMessage("Wrong password.");
						return null;
					}
					else
					{
						SetSuccessMessage("Correct password.");
						keyManager.SetPasswordVerified();
					}
				}

				return keyManager;
			}
			catch (Exception ex)
			{
				// Initialization failed.
				SetValidationMessage(ex.ToTypeMessageString());
				Logger.LogError<LoadWalletViewModel>(ex);

				return null;
			}
			finally
			{
				CanTestPassword = IsWalletSelected;
			}
		}

		private static bool TryFindWalletByMasterFingerprint(HDFingerprint masterFingerprint, out string walletName)
		{
			// Start searching for the real wallet name.
			walletName = null;

			var walletFiles = new DirectoryInfo(Global.WalletsDir);
			var walletBackupFiles = new DirectoryInfo(Global.WalletBackupsDir);

			List<FileInfo> walletFileNames = new List<FileInfo>();

			if (walletFiles.Exists)
			{
				walletFileNames.AddRange(walletFiles.EnumerateFiles());
			}

			if (walletBackupFiles.Exists)
			{
				walletFileNames.AddRange(walletFiles.EnumerateFiles());
			}

			walletFileNames = walletFileNames.OrderByDescending(x => x.LastAccessTimeUtc).ToList();

			foreach (FileInfo walletFile in walletFileNames)
			{
				if (walletFile?.Extension?.Equals(".json", StringComparison.OrdinalIgnoreCase) is true
					&& KeyManager.TryGetMasterFingerprintFromFile(walletFile.FullName, out HDFingerprint fp))
				{
					if (fp == masterFingerprint) // We already had it.
					{
						walletName = walletFile.Name;
						return true;
					}
				}
			}

			return false;
		}

		public async Task LoadWalletAsync()
		{
			try
			{
				IsBusy = true;

				var keyManager = await LoadKeyManagerAsync(IsPasswordRequired, IsHardwareWallet);
				if (keyManager is null)
				{
					return;
				}

				try
				{
					await Task.Run(async () =>
					{
						await Global.InitializeWalletServiceAsync(keyManager);
					});
					// Successffully initialized.
					Owner.OnClose();
					// Open Wallet Explorer tabs
					if (Global.WalletService.Coins.Any())
					{
						// If already have coins then open with History tab first.
						IoC.Get<WalletExplorerViewModel>().OpenWallet(Global.WalletService, receiveDominant: false);
					}
					else // Else open with Receive tab first.
					{
						IoC.Get<WalletExplorerViewModel>().OpenWallet(Global.WalletService, receiveDominant: true);
					}
				}
				catch (Exception ex)
				{
					// Initialization failed.
					SetValidationMessage(ex.ToTypeMessageString());
					Logger.LogError<LoadWalletViewModel>(ex);
					await Global.DisposeInWalletDependentServicesAsync();
				}
			}
			finally
			{
				IsBusy = false;
				SetWalletStates();
			}
		}

		public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }

		public void OpenWalletsFolder()
		{
			var path = Global.WalletsDir;
			IoHelpers.OpenFolderInFileExplorer(path);
		}
	}
}
