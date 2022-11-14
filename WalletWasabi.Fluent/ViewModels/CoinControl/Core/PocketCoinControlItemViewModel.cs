using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

internal class PocketCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public PocketCoinControlItemViewModel(Pocket pocket) : base(pocket.Coins.OrderByDescending(x => x.Amount).Select(coin => new CoinCoinControlItemViewModel(coin)).ToList())
	{
		Amount = pocket.Amount;
		IsConfirmed = pocket.Coins.All(x => x.Confirmed);
		IsCoinjoining = pocket.Coins.Any(x => x.CoinJoinInProgress);
		AnonymityScore = null;
		Labels = pocket.Labels;
	}

	public override bool IsConfirmed { get; }
	public override bool IsCoinjoining { get; }
	public override bool IsBanned => false;
	public override string ConfirmationStatus => "";
	public override Money Amount { get; }
	public override string BannedUntilUtcToolTip => "";
	public override int? AnonymityScore { get; }
	public override SmartLabel Labels { get; }
	public override DateTimeOffset? BannedUntilUtc => null;
}
