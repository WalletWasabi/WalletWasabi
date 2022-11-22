using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

internal class CoinCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public CoinCoinControlItemViewModel(SmartCoin smartCoin)
	{
		Amount = smartCoin.Amount;
		IsConfirmed = smartCoin.Confirmed;
		IsBanned = smartCoin.IsBanned;
		IsCoinjoining = smartCoin.CoinJoinInProgress;
		ConfirmationStatus = $"{smartCoin.Height} confirmation{TextHelpers.AddSIfPlural(smartCoin.Height)}";
		BannedUntilUtcToolTip = smartCoin.BannedUntilUtc.HasValue ? $"Can't participate in coinjoin until: {smartCoin.BannedUntilUtc:g}" : "";
		AnonymityScore = (int) smartCoin.HdPubKey.AnonymitySet;
		Labels = smartCoin.HdPubKey.Label;
		BannedUntilUtc = smartCoin.BannedUntilUtc;
		Children = ImmutableList<CoinControlItemViewModelBase>.Empty;
	}
}
