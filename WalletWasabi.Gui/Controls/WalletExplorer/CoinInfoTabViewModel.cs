using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public CoinInfoTabViewModel(CoinViewModel coin) : base(string.Empty)
		{
			Coin = coin;
			Title = $"Details of {coin.OutputIndex}:{coin.TransactionId[0..7]}";
		}

		public CoinViewModel Coin { get; }
	}
}
