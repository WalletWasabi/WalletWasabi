using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
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
			IsWalletOpened = false;
			ValidationMessage = null;
		}

		public ReactiveCommand LoadCommand { get; }

		public async Task LoadWalletAsync()
		{
		}
	}
}
