using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins
{
	public partial class WalletCoinViewModel : ViewModelBase
	{
		[AutoNotify] private Money _amount;
		[AutoNotify] private int _anonymitySet;
		[AutoNotify] private SmartLabel _smartLabel;
		[AutoNotify] private bool _confirmed;

		public WalletCoinViewModel(SmartCoin coin)
		{
			Amount = coin.Amount;
			AnonymitySet = coin.HdPubKey.AnonymitySet;
			SmartLabel = coin.HdPubKey.Cluster.Labels;
			Confirmed = coin.Confirmed;
		}
	}
}
