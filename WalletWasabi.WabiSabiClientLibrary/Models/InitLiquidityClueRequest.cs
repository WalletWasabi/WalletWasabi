using NBitcoin;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record InitLiquidityClueRequest(
	Money[] ExternalAmounts
);
