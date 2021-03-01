using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public partial class PocketViewModel : ViewModelBase
	{
		[AutoNotify] private bool _isSelected;
		[AutoNotify] private decimal _totalBtc;
		[AutoNotify] private string[] _labels;

		public PocketViewModel((string[] labels, ICoinsView coins) pocket)
		{
			Coins = pocket.coins;
			_labels = pocket.labels;
			_totalBtc = pocket.coins.TotalAmount().ToDecimal(MoneyUnit.BTC);
		}

		public ICoinsView Coins { get; }
	}
}