using System.Linq;
using Avalonia;
using Avalonia.Controls;
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

	public InputsCoinViewModel(InputsCoinViewModel[] coins, int inputCount, bool isExpanded, int? nbDiff)
	{
		Amount = new Amount(coins.Sum(x => x.Amount.Btc));
		Children = coins;
		TotalInputs = inputCount;
		IsExpanded = isExpanded;
		NbDiff = nbDiff;
		if (IsExpanded)
		{
			foreach (var coin in Children)
			{
				coin.IsExpanded = true;
			}
		}

		if (Children.Count == TotalInputs)
		{
			TitleText = $"{Children.Count} input{(Children.Count == 1 ? "" : "s")}";
			Margin = new Thickness(45, 0, 0, 0);
		}
		else
		{
			TitleText = $"{Children.Count} own out of {TotalInputs} input{(TotalInputs == 1 ? "" : "s")}";
			Tip = "Only own inputs are known.";
			Margin = new Thickness(15, 0, 0, 0);
		}
	}
	public SmartCoin? Coin { get; }
}
