using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class CoinCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public CoinCoinControlItemViewModel(SmartCoin smartCoin)
	{
		SmartCoin = smartCoin;
		Amount = smartCoin.Amount;
		IsConfirmed = smartCoin.Confirmed;
		IsBanned = smartCoin.IsBanned;
		IsCoinjoining = smartCoin.CoinJoinInProgress;
		var confirmationCount = smartCoin.GetConfirmations();
		ConfirmationStatus = $"{confirmationCount} confirmation{TextHelpers.AddSIfPlural(confirmationCount)}";
		BannedUntilUtcToolTip = smartCoin.BannedUntilUtc.HasValue ? $"Can't participate in coinjoin until: {smartCoin.BannedUntilUtc:g}" : null;
		AnonymityScore = (int)smartCoin.HdPubKey.AnonymitySet;
		BannedUntilUtc = smartCoin.BannedUntilUtc;
		IsSelected = false;
		ScriptType = ScriptType.FromEnum(smartCoin.ScriptType);
	}

	public SmartCoin SmartCoin { get; }
}
