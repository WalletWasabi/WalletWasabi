using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using NBitcoin;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.WalletExplorer;
using Avalonia.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class LoadWalletViewModel : CategoryViewModel
	{
		private ObservableCollection<string> _wallets;
		private string _selectedWallet;
		private bool _isWalletOpened;
		private bool _canLoadWallet;
		private string _warningMessage;
		private string _validationMessage;
		private bool _isBusy;
		private string _loadButtonText;

		private WalletManagerViewModel Owner { get; }

		public LoadWalletViewModel(WalletManagerViewModel owner) : base("Load Wallet")
		{
			Owner = owner;
			_wallets = new ObservableCollection<string>();

			this.WhenAnyValue(x => x.SelectedWallet)
				.Subscribe(selectedWallet => CanLoadWallet = !string.IsNullOrEmpty(selectedWallet) && !IsWalletOpened);

			this.WhenAnyValue(x => x.IsWalletOpened)
				.Subscribe(isWalletOpened => CanLoadWallet = !string.IsNullOrEmpty(SelectedWallet) && !isWalletOpened);

			this.WhenAnyValue(x => x.IsWalletOpened)
				.Subscribe(isWalletOpened => WarningMessage = isWalletOpened
					? "There is already an open wallet. Restart the application to open another one."
					: string.Empty);

			LoadCommand = ReactiveCommand.Create(LoadWalletAsync, this.WhenAnyValue(x => x.CanLoadWallet));
			OpenFolderCommand = ReactiveCommand.Create(OpenWalletsFolder);
			SetLoadButtonText(IsBusy);
		}

		public ObservableCollection<string> Wallets
		{
			get { return _wallets; }
			set { this.RaiseAndSetIfChanged(ref _wallets, value); }
		}

		public string SelectedWallet
		{
			get { return _selectedWallet; }
			set { this.RaiseAndSetIfChanged(ref _selectedWallet, value); }
		}

		public bool IsWalletOpened
		{
			get { return _isWalletOpened; }
			set { this.RaiseAndSetIfChanged(ref _isWalletOpened, value); }
		}

		public string WarningMessage
		{
			get { return _warningMessage; }
			set { this.RaiseAndSetIfChanged(ref _warningMessage, value); }
		}

		public string ValidationMessage
		{
			get { return _validationMessage; }
			set { this.RaiseAndSetIfChanged(ref _validationMessage, value); }
		}

		public void SetLoadButtonText(bool isBusy)
		{
			LoadButtonText = isBusy ? "Loading..." : "Load Wallet";
		}

		public string LoadButtonText
		{
			get { return _loadButtonText; }
			set { this.RaiseAndSetIfChanged(ref _loadButtonText, value); }
		}

		public bool CanLoadWallet
		{
			get
			{
				if (IsBusy) return false;
				return _canLoadWallet;
			}
			set
			{
				this.RaiseAndSetIfChanged(ref _canLoadWallet, value);
			}
		}

		public bool IsBusy
		{
			get { return _isBusy; }
			set
			{
				CanLoadWallet = !value;
				SetLoadButtonText(value);
				this.RaiseAndSetIfChanged(ref _isBusy, value);
			}
		}

		public override void OnCategorySelected()
		{
			_wallets.Clear();

			if (!File.Exists(Global.WalletsDir))
			{
				Directory.CreateDirectory(Global.WalletsDir);
			}

			var directoryInfo = new DirectoryInfo(Global.WalletsDir);
			var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
			foreach (var file in walletFiles)
			{
				_wallets.Add(Path.GetFileNameWithoutExtension(file.FullName));
			}

			if (_wallets.Any())
			{
				SelectedWallet = _wallets.First();
			}

			IsWalletOpened = !(Global.WalletService is null);
			ValidationMessage = null;
		}

		public ReactiveCommand LoadCommand { get; }

		public async Task LoadWalletAsync()
		{
			try
			{
				IsBusy = true;

				var walletFullPath = Path.Combine(Global.WalletsDir, SelectedWallet + ".json");
				var walletBackupFullPath = Path.Combine(Global.WalletBackupsDir, SelectedWallet + ".json");
				if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
				{
					// The selected wallet is not available any more (someone deleted it?).
					OnCategorySelected();
					SelectedWallet = null;
					ValidationMessage = "The selected wallet and its backup doesn't exsist, did you delete them?";
					return;
				}

				try
				{
					await Task.Run(async () =>
					{
						KeyManager keyManager = null;
						try
						{
							keyManager = LoadWallet(walletFullPath);
						}
						catch (Exception ex)
						{
							if (!File.Exists(walletBackupFullPath))
							{
								throw;
							}

							Logger.LogWarning($"Wallet got corrupted.\n" +
								$"Wallet Filepath: {walletFullPath}\n" +
								$"Trying to recover it from backup.\n" +
								$"Backup path: {walletBackupFullPath}\n" +
								$"Exception: {ex.ToString()}");
							if (File.Exists(walletFullPath))
							{
								string corruptedWalletBackupPath = Path.Combine(Global.WalletBackupsDir, $"{Path.GetFileName(walletFullPath)}_CorruptedBackup");
								if (File.Exists(corruptedWalletBackupPath))
								{
									File.Delete(corruptedWalletBackupPath);
									Logger.LogInfo($"Deleted previous corrupted wallet file backup from {corruptedWalletBackupPath}.");
								}
								File.Move(walletFullPath, corruptedWalletBackupPath);
								Logger.LogInfo($"Backed up corrupted wallet file to {corruptedWalletBackupPath}.");
							}
							File.Copy(walletBackupFullPath, walletFullPath);

							keyManager = LoadWallet(walletFullPath);
						}

						await Global.InitializeWalletServiceAsync(keyManager);
					});

					// Successffully initialized.
					IoC.Get<IShell>().RemoveDocument(Owner);
					// Open Wallet Explorer tabs
					if (Global.WalletService.Coins.Any())
					{
						// If already have coins then open with History tab first.
						IoC.Get<WalletExplorerViewModel>().OpenWallet(SelectedWallet, receiveDominant: false);
					}
					else // Else open with Receive tab first.
					{
						IoC.Get<WalletExplorerViewModel>().OpenWallet(SelectedWallet, receiveDominant: true);
					}
				}
				catch (Exception ex)
				{
					// Initialization failed.
					ValidationMessage = ex.ToTypeMessageString();
					Logger.LogError<LoadWalletViewModel>(ex);
					await Global.DisposeInWalletDependentServicesAsync();
				}
			}
			finally
			{
				IsBusy = false;
			}
		}

		private KeyManager LoadWallet(string walletFullPath)
		{
			KeyManager keyManager;
			var walletFileInfo = new FileInfo(walletFullPath);
			walletFileInfo.LastAccessTime = DateTime.Now;

			keyManager = KeyManager.FromFile(walletFullPath);
			Logger.LogInfo($"Wallet decrypted: {SelectedWallet}.");
			return keyManager;
		}

		public ReactiveCommand OpenFolderCommand { get; }

		public void OpenWalletsFolder()
		{
			var path = Global.WalletsDir;
			IoHelpers.OpenFolderInFileExplorer(path);
		}
	}
}
