using NBitcoin;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record GetLiquidityClueRequest(
	Money? RawLiquidityClue,
	Money MaxSuggestedAmount
);
