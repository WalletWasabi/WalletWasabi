using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public CoinInfoTabViewModel(CoinViewModel coin) : base(string.Empty)
		{
			Coin = coin;
			Title = $"{coin.TransactionId[0..7]}'s Details";
		}

		public CoinViewModel Coin { get; }
	}
}
