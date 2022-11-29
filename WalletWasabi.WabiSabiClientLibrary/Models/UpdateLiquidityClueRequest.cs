using NBitcoin;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record UpdateLiquidityClueRequest(
	Money? RawLiquidityClue,
	Money MaxSuggestedAmount,
	Money[] ExternalAmounts
);
