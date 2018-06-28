using System.Collections.Generic;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModelBase : ViewModelBase
	{
		private ICollection<CoinViewModel> _selectedCoins;

		public CoinListViewModelBase()
		{
			SelectedCoins = new ObservableCollection<CoinViewModel>();
		}

		public ICollection<CoinViewModel> SelectedCoins
		{
			get { return _selectedCoins; }
			set { this.RaiseAndSetIfChanged(ref _selectedCoins, value); }
		}
	}
}
