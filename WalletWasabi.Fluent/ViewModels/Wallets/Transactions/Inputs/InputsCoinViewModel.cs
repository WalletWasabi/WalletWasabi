using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public class InputsCoinViewModel : InputsCoinListItem
{
    public InputsCoinViewModel(SmartCoin coin, Network network)
	{
		Coin = coin;
		Amount = new Amount(coin.Amount);
		BtcAddress = coin.ScriptPubKey.GetDestinationAddress(network)?.ToString();
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

		TitleText = $"{TotalInputs} {Lang.Utils.LowerCaseFirst(Lang.Utils.PluralIfNeeded(TotalInputs.GetValueOrDefault(), "Words_Input")!)}";

		Tip = Children.Count == TotalInputs ?
			Lang.Resources.InputsCoinViewModel_Tip_AllInputs :
			Lang.Resources.InputsCoinViewModel_Tip_SubsetInputs;
	}
	public SmartCoin? Coin { get; }
}
