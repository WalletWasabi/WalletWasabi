using ReactiveUI;
using ReactiveUI.Legacy;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListViewModel : ViewModelBase
	{
#pragma warning disable CS0618 // Type or member is obsolete
		private IReactiveDerivedList<CoinViewModel> _coins;

		public CoinListViewModel(IReactiveDerivedList<CoinViewModel> coins)
		{
			Coins = coins;
		}

		public IReactiveDerivedList<CoinViewModel> Coins
		{
			get { return _coins; }
			set { this.RaiseAndSetIfChanged(ref _coins, value); }
		}
#pragma warning restore CS0618 // Type or member is obsolete
	}
}
