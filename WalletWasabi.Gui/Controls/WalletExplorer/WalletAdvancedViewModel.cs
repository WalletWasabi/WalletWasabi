using ReactiveUI;
using System.Collections.ObjectModel;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletAdvancedViewModel : WalletActionViewModel
	{
		private ObservableCollection<WalletActionViewModel> _items;

		public WalletAdvancedViewModel(WalletViewModel walletViewModel) : base(walletViewModel.Name, walletViewModel)
		{
			Items = new ObservableCollection<WalletActionViewModel>();
		}

		public ObservableCollection<WalletActionViewModel> Items
		{
			get => _items;
			set => this.RaiseAndSetIfChanged(ref _items, value);
		}
	}
}
