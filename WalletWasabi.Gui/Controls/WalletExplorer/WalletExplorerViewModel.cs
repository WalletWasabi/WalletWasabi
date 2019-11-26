using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using WalletWasabi.Gui.Extensions;
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

			_wallets = new ObservableCollection<WalletViewModelBase>();
		}

		private ObservableCollection<WalletViewModelBase> _wallets;

		public ObservableCollection<WalletViewModelBase> Wallets
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
			var toRemove = Wallets.OfType<ClosedWalletViewModel>()
				.Where(x => Path.GetFullPath(x.WalletFile).Equals(Path.GetFullPath(walletService.KeyManager.FilePath)))
				.ToList();

			foreach(var wallet in toRemove)
			{
				_wallets.Remove(wallet);
			}

			var walletName = Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath);
			if (_wallets.Any(x => x.Title == walletName))
			{
				return;
			}

			WalletViewModel walletViewModel = new WalletViewModel(receiveDominant, walletService);
			_wallets.InsertSorted(walletViewModel);
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

			var global = Locator.Current.GetService<Global>();

			var directoryInfo = new DirectoryInfo(global.WalletsDir);
			var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);

			foreach (var file in walletFiles)
			{
				var wallet = new ClosedWalletViewModel(file.FullName);

				_wallets.InsertSorted(wallet);
			}
		}
	}
}
