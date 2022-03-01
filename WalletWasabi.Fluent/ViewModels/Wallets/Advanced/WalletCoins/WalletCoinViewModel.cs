using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

public partial class WalletCoinViewModel : ViewModelBase
{
	[AutoNotify] private Money _amount;
	[AutoNotify] private int _anonymitySet;
	[AutoNotify] private SmartLabel _smartLabel;
	[AutoNotify] private bool _confirmed;
	[AutoNotify] private bool _coinJoinInProgress;

	public WalletCoinViewModel(SmartCoin coin)
	{
		Amount = coin.Amount;

		coin.WhenAnyValue(c => c.Confirmed).Subscribe(x => Confirmed = x);
		coin.WhenAnyValue(c => c.HdPubKey.Cluster.Labels).Subscribe(x => SmartLabel = x);
		coin.WhenAnyValue(c => c.HdPubKey.AnonymitySet).Subscribe(x => AnonymitySet = x);
		coin.WhenAnyValue(c => c.CoinJoinInProgress).Subscribe(x => CoinJoinInProgress = x);
	}

	public static Comparison<WalletCoinViewModel?> SortAscending<T>(Func<WalletCoinViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return -1;
			}
			else if (y is null)
			{
				return 1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(x), selector(y));
			}
		};
	}

	public static Comparison<WalletCoinViewModel?> SortDescending<T>(Func<WalletCoinViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return 1;
			}
			else if (y is null)
			{
				return -1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(y), selector(x));
			}
		};
	}
}
