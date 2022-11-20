using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

internal class PocketCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public PocketCoinControlItemViewModel(Pocket pocket)
	{
		var confirmationCount = pocket.Coins.Count();
		var unconfirmedCount = pocket.Coins.Count(x => !x.Confirmed);
		var allConfirmed = confirmationCount == unconfirmedCount;
		ConfirmationStatus = allConfirmed ? "All coins are confirmed" : $"{unconfirmedCount} coins are waiting for confirmation";
		Amount = pocket.Amount;
		IsConfirmed = allConfirmed;
		IsCoinjoining = pocket.Coins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = (int) pocket.Coins.Max(x => x.HdPubKey.AnonymitySet);
		Labels = pocket.Labels;
		Children = pocket.Coins.OrderByDescending(x => x.Amount).Select(coin => new CoinCoinControlItemViewModel(coin)).ToList();
	}

	public override IReadOnlyCollection<CoinControlItemViewModelBase> Children { get; }
	public override bool IsConfirmed { get; }
	public override bool IsCoinjoining { get; }
	public override bool IsBanned => false;
	public override string ConfirmationStatus { get; }
	public override Money Amount { get; }
	public override string BannedUntilUtcToolTip => "";
	public override int AnonymityScore { get; }
	public override SmartLabel Labels { get; }
	public override DateTimeOffset? BannedUntilUtc => null;
}
