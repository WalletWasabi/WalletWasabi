using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public partial class PocketViewModel : ViewModelBase
	{
		[AutoNotify] private bool _isSelected;
		[AutoNotify] private decimal _totalBtc;
		[AutoNotify] private SmartLabel _labels;

		public PocketViewModel((SmartLabel labels, ICoinsView coins) pocket)
		{
			Coins = pocket.coins;
			_labels = pocket.labels;
			_totalBtc = pocket.coins.TotalAmount().ToDecimal(MoneyUnit.BTC);
		}

		public ICoinsView Coins { get; }
	}
}
