using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

public class CoinjoinCoinViewModel : CoinjoinCoinListItem
{
    public CoinjoinCoinViewModel(SmartCoin coin)
	{
		Coin = coin;
		Amount = coin.Amount;
		if(coin.HdPubKey.HistoricalAnonSet.TryGetValue(coin.Outpoint.Hash, out var anonSetWhenTxProcessed))
		{
			AnonymityScore = (int)anonSetWhenTxProcessed;
		}
		else
		{
			AnonymityScore = (int)coin.AnonymitySet;
		}
	}

	public CoinjoinCoinViewModel(CoinjoinCoinViewModel[] coins, int coinjoinInputCount)
	{
		Amount = coins.Sum(x => x.Amount);
		Children = coins;
		TotalCoinsOnSideCount = coinjoinInputCount;
		IsExpanded = false;
	}

	public SmartCoin? Coin { get; }
}
