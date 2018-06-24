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

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class LoadWalletViewModel : CategoryViewModel
	{
		private ObservableCollection<string> _wallets;
		private string _selectedWallet;
		private bool _isSelectedWallet;

		public LoadWalletViewModel() : base("Load Wallet")
		{
			_wallets = new ObservableCollection<string>();
			this.WhenAnyValue(x => x.SelectedWallet).Subscribe(SelectedWallet => IsSelectedWallet = !string.IsNullOrEmpty(SelectedWallet));
			LoadCommand = ReactiveCommand.Create(
				LoadWallet,
				this.WhenAnyValue(x => x.IsSelectedWallet));
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

		public bool IsSelectedWallet
		{
			get { return _isSelectedWallet; }
			set { this.RaiseAndSetIfChanged(ref _isSelectedWallet, value); }
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
		}

		public ReactiveCommand LoadCommand { get; }

		public void LoadWallet()
		{
			var walletFullPath = Path.Combine(Global.WalletsDir, SelectedWallet + ".json");
			if (!File.Exists(walletFullPath))
			{
				// the selected wallets is not available any more (someone deleted it)
				OnCategorySelected();
				SelectedWallet = null;
				return;
			}

			var keyManager = KeyManager.FromFile(walletFullPath);

			Global.InitializeWalletService(keyManager);
		}
	}
}
