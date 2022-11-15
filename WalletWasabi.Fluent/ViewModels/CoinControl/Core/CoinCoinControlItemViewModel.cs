using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

internal class CoinCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public CoinCoinControlItemViewModel(SmartCoin smartCoin)
	{
		Amount = smartCoin.Amount;
		IsConfirmed = smartCoin.Confirmed;
		IsBanned = smartCoin.IsBanned;
		ConfirmationStatus = "";
		IsCoinjoining = smartCoin.CoinJoinInProgress;
		ConfirmationStatus = smartCoin.Confirmed ? "Confirmed" : "Pending confirmation";
		BannedUntilUtcToolTip = smartCoin.BannedUntilUtc.HasValue ? $"Can't participate in coinjoin until: {smartCoin.BannedUntilUtc:g}" : "";
		AnonymityScore = (int) smartCoin.HdPubKey.AnonymitySet;
		Labels = smartCoin.HdPubKey.Label;
		BannedUntilUtc = smartCoin.BannedUntilUtc;
	}

	public override DateTimeOffset? BannedUntilUtc { get; }
	public override bool IsConfirmed { get; }
	public override bool IsCoinjoining { get; }
	public override bool IsBanned { get; }
	public override string ConfirmationStatus { get; }
	public override Money Amount { get; }
	public override string BannedUntilUtcToolTip { get; }
	public override int AnonymityScore { get; }
	public override SmartLabel Labels { get; }
}
