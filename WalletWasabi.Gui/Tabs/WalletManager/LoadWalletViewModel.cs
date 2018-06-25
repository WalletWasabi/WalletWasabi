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

		public bool CanLoadWallet
		{
			get { return _canLoadWallet; }
			set { this.RaiseAndSetIfChanged(ref _canLoadWallet, value); }
		}

		public override void OnCategorySelected()
		{
			_wallets.Clear();

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

			IsWalletOpened = Global.WalletService != null;
			ValidationMessage = null;
		}

		public ReactiveCommand LoadCommand { get; }

		public async Task LoadWalletAsync()
		{
			var walletFullPath = Path.Combine(Global.WalletsDir, SelectedWallet + ".json");
			if (!File.Exists(walletFullPath))
			{
				// the selected wallets is not available any more (someone deleted it)
				OnCategorySelected();
				SelectedWallet = null;
				return;
			}

			try
			{
				var keyManager = KeyManager.FromFile(walletFullPath);
				await Global.InitializeWalletServiceAsync(keyManager);
				// Successffully initialized.
				IoC.Get<IShell>().RemoveDocument(Owner);
				// Open Wallet Explorer tabs
				IoC.Get<WalletExplorerViewModel>().OpenWallet(SelectedWallet);
			}
			catch (Exception ex)
			{
				// Initialization failed.
				ValidationMessage = ex.ToTypeMessageString();
				Logger.LogError<LoadWalletViewModel>(ex);
				await Global.DisposeInWalletDependentServicesAsync();
			}
		}
	}
}
