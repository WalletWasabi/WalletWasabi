using System.Linq;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class PocketCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public PocketCoinControlItemViewModel(Pocket pocket)
	{
		var unconfirmedCount = pocket.Coins.Count(x => !x.Confirmed);
		IsConfirmed = unconfirmedCount == 0;
		ConfirmationStatus = IsConfirmed ? "All coins are confirmed" : $"{unconfirmedCount} coins are waiting for confirmation";
		IsBanned = pocket.Coins.Any(x => x.IsBanned);
		BannedUntilUtcToolTip = IsBanned ? "Some coins can't participate in coinjoin" : null;
		Amount = pocket.Amount;
		IsCoinjoining = pocket.Coins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = (int)pocket.Coins.Max(x => x.HdPubKey.AnonymitySet);
		Labels = pocket.Labels;
		Children = pocket.Coins.OrderByDescending(x => x.Amount).Select(coin => new CoinCoinControlItemViewModel(coin)).ToList();
	}
}
