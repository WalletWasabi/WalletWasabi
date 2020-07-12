using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public CoinInfoTabViewModel(CoinViewModel coin) : base("")
		{
			Coin = coin;
			Title = $"Coin ({coin.Amount.ToString(false, true)}) Details";
		}

		public CoinViewModel Coin { get; }
	}
}
