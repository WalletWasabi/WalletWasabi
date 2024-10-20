using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public class InputsCoinViewModel : InputsCoinListItem
{
    public InputsCoinViewModel(SmartCoin coin)
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

	public InputsCoinViewModel(InputsCoinViewModel[] coins, int inputCount, bool isExpanded, int? oldInputCount)
	{
		Amount = new Amount(coins.Sum(x => x.Amount.Btc));
		Children = coins;
		TotalInputs = inputCount;
		IsExpanded = isExpanded;
		if (IsExpanded)
		{
			foreach (var coin in Children)
			{
				coin.IsExpanded = true;
			}
		}
		TitleText = Children.Count == TotalInputs ?
			$"{Children.Count} input{(Children.Count == 1 ? "" : "s")}" :
			$"{Children.Count} out of {TotalInputs} input{(Children.Count == 1 ? "" : "s")}";

		if (oldInputCount is not null)
		{
			NbDiff = inputCount - oldInputCount;
		}
	}
	public SmartCoin? Coin { get; }
}
