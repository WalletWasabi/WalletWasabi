using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Text;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using System.Linq;
using AvalonStudio.Shell;

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

		internal void OpenWallet(string walletName, bool receiveDominant)
		{
			if (_wallets.Any(x => x.Title == walletName))
				return;

			WalletViewModel walletViewModel = new WalletViewModel(walletName, receiveDominant);
			_wallets.Add(walletViewModel);
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
