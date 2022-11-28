using System.Linq;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class PocketCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public PocketCoinControlItemViewModel(Pocket pocket)
	{
		var confirmedCount = pocket.Coins.Count();
		var unconfirmedCount = pocket.Coins.Count(x => !x.Confirmed);
		var allConfirmed = confirmedCount == unconfirmedCount;
		ConfirmationStatus = allConfirmed ? "All coins are confirmed" : $"{unconfirmedCount} coins are waiting for confirmation";
		IsBanned = pocket.Coins.Any(x => x.IsBanned);
		BannedUntilUtcToolTip = IsBanned ? "Some coins can't participate in coinjoin" : null;
		Amount = pocket.Amount;
		IsConfirmed = allConfirmed;
		IsCoinjoining = pocket.Coins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = (int) pocket.Coins.Max(x => x.HdPubKey.AnonymitySet);
		Labels = pocket.Labels;
		Children = pocket.Coins.OrderByDescending(x => x.Amount).Select(coin => new CoinCoinControlItemViewModel(coin)).ToList();
	}
}
