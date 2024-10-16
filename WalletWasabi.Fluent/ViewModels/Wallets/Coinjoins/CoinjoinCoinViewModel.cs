using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

public class CoinjoinCoinViewModel : CoinjoinCoinListItem
{
    public CoinjoinCoinViewModel(SmartCoin coin)
	{
		Coin = coin;
		Amount = new Amount(coin.Amount);
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
		Amount = new Amount(coins.Sum(x => x.Amount.Btc));
		Children = coins;
		TotalCoinsOnSideCount = coinjoinInputCount;
		IsExpanded = false;
		TitleText = $"{Children.Count} out of {TotalCoinsOnSideCount}";
	}
	public SmartCoin? Coin { get; }
}
