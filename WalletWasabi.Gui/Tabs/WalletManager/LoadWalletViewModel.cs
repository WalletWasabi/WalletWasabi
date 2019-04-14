using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class LoadWalletViewModel : CategoryViewModel
	{
		private ObservableCollection<string> _wallets;
		private string _password;
		private string _selectedWallet;
		private bool _isWalletSelected;
		private bool _isWalletOpened;
		private bool _canLoadWallet;
		private bool _canTestPassword;
		private string _warningMessage;
		private string _validationMessage;
		private string _successMessage;
		private bool _isBusy;
		private string _loadButtonText;

		private WalletManagerViewModel Owner { get; }
		public LoadWalletType LoadWalletType { get; }

		public bool IsPasswordRequired => LoadWalletType == LoadWalletType.Password;
		public bool IsHardwareWallet => LoadWalletType == LoadWalletType.Hardware;
		public bool IsDesktopWallet => LoadWalletType == LoadWalletType.Desktop;

		public LoadWalletViewModel(WalletManagerViewModel owner, LoadWalletType loadWalletType) : base(loadWalletType == LoadWalletType.Password ? "Test Password" : (loadWalletType == LoadWalletType.Desktop ? "Load Wallet" : "Hardware Wallet"))
		{
			Owner = owner;
			Password = "";
			LoadWalletType = loadWalletType;
			Wallets = new ObservableCollection<string>();

			this.WhenAnyValue(x => x.SelectedWallet)
				.Subscribe(selectedWallet => SetWalletStates());

			this.WhenAnyValue(x => x.IsWalletOpened)
				.Subscribe(isWalletOpened => SetWalletStates());

			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				try
				{
					if (x.NotNullAndNotEmpty())
					{
						char lastChar = x.Last();
						if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
						{
							Password = x.TrimEnd('\r', '\n');
							LoadKeyManager(requirePassword: true);
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogTrace(ex);
				}
			});

			LoadCommand = ReactiveCommand.CreateFromTask(LoadWalletAsync, this.WhenAnyValue(x => x.CanLoadWallet));
			TestPasswordCommand = ReactiveCommand.Create(() => LoadKeyManager(requirePassword: true), this.WhenAnyValue(x => x.CanTestPassword));
			OpenFolderCommand = ReactiveCommand.Create(OpenWalletsFolder);
			SetLoadButtonText(IsBusy);
		}

		public ObservableCollection<string> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string SelectedWallet
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

		private void SetValidationMessage(string message)
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

				if (value)
				{
					MainWindowViewModel.Instance.StatusBar.SetStatusAndDoUpdateActions("Loading...");
				}
				else
				{
					MainWindowViewModel.Instance.StatusBar.SetStatusAndDoUpdateActions();
				}
			}
		}

		public override void OnCategorySelected()
		{
			Wallets.Clear();
			Password = "";
			SetValidationMessage("");

			if (IsHardwareWallet)
			{
				Dispatcher.UIThread.PostLogException(async () =>
				{
					IEnumerable<JObject> res = await HwiProcessManager.EnumerateAsync();
					if (res.Any())
					{
						var uniqueTypes = new HashSet<string>();
						foreach (JObject hw in res)
						{
							var type = hw.Value<string>("type");
							uniqueTypes.Add(type);
						}

						var alltypesunique = res.Count() == uniqueTypes.Count();

						foreach (JObject hw in res)
						{
							var type = hw.Value<string>("type");
							var fingerprint = hw.Value<string>("fingerprint");
							var display = new StringBuilder(type);
							if (!alltypesunique)
							{
								display.Append($", fingerprint: {fingerprint}");
							}

							Wallets.Add(display.ToString());
						}

						SelectedWallet = Wallets.FirstOrDefault();
						SetWalletStates();
					}
				});
			}
			else
			{
				if (!File.Exists(Global.WalletsDir))
				{
					Directory.CreateDirectory(Global.WalletsDir);
				}

				var directoryInfo = new DirectoryInfo(Global.WalletsDir);
				var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
				foreach (var file in walletFiles)
				{
					Wallets.Add(Path.GetFileNameWithoutExtension(file.FullName));
				}
			}

			SelectedWallet = Wallets.FirstOrDefault();
			SetWalletStates();
		}

		private void SetWalletStates()
		{
			IsWalletSelected = !string.IsNullOrEmpty(SelectedWallet);
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

		public KeyManager LoadKeyManager(bool requirePassword)
		{
			try
			{
				CanTestPassword = false;
				var password = Guard.Correct(Password); // Don't let whitespaces to the beginning and to the end.
				Password = ""; // Clear password field.

				if (string.IsNullOrEmpty(SelectedWallet))
				{
					SetValidationMessage("No wallet selected.");
					return null;
				}

				var walletFullPath = Global.GetWalletFullPath(SelectedWallet);
				var walletBackupFullPath = Global.GetWalletBackupFullPath(SelectedWallet);
				if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
				{
					// The selected wallet is not available any more (someone deleted it?).
					OnCategorySelected();
					SetValidationMessage("The selected wallet and its backup don't exist, did you delete them?");
					return null;
				}

				KeyManager keyManager = Global.LoadKeyManager(walletFullPath, walletBackupFullPath);

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

		public async Task LoadWalletAsync()
		{
			try
			{
				IsBusy = true;

				var keyManager = LoadKeyManager(IsPasswordRequired);
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
					IoC.Get<IShell>().RemoveDocument(Owner);
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
