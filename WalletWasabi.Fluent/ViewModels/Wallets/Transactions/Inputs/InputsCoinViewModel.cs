using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public class InputsCoinViewModel : InputsCoinListItem
{
	public InputsCoinViewModel(UiContext uiContext, SmartCoin coin, Network network) : base(uiContext)
	{
		Coin = coin;
		Amount = new Amount(coin.Amount);
		BtcAddress = coin.ScriptPubKey.GetDestinationAddress(network)?.ToString();
		TitleText = "";
		AnonymityScore = (int)coin.AnonymitySet;
	}

	public InputsCoinViewModel(UiContext uiContext, InputsCoinViewModel[] coins, int inputCount, bool isExpanded, int? nbDiff) : base(uiContext)
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

		TitleText = $"{TotalInputs} input{(TotalInputs == 1 ? "" : "s")}";

		Tip = Children.Count == TotalInputs ?
			"All inputs belong to one of your opened wallets" :
			"Only inputs belonging to one of your opened wallets can be shown.";
	}
	public SmartCoin? Coin { get; }
}
