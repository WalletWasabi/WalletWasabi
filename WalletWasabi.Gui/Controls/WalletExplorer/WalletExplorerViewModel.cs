using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[Export]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IActivatableExtension
	{
		public override Location DefaultLocation => Location.Right;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModel>();
		}

		[Import]
		public AvaloniaGlobalComponent GlobalComponent { get; set; }

		public Global Global => GlobalComponent?.Global;

		private ObservableCollection<WalletViewModel> _wallets;

		public ObservableCollection<WalletViewModel> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		private WasabiDocumentTabViewModel _selectedItem;

		public WasabiDocumentTabViewModel SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		internal void OpenWallet(WalletService walletService, bool receiveDominant)
		{
			var walletName = Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath);
			if (_wallets.Any(x => x.Title == walletName))
			{
				return;
			}

			WalletViewModel walletViewModel = new WalletViewModel(Global, receiveDominant);
			_wallets.Add(walletViewModel);
			walletViewModel.OnWalletOpened();

			// TODO if we ever implement closing a wallet OnWalletClosed needs to be called
			// to prevent memory leaks.
		}

		public void BeforeActivation()
		{
		}

		public void Activation()
		{
			IoC.Get<IShell>().MainPerspective.AddOrSelectTool(this);
		}
	}
}
