namespace WalletWasabi.WabiSabiClientLibrary.Models;

/// <summary>
/// Response object for <see cref="GetOutputAmountsRequest"/>.
/// </summary>
/// <param name="OutputAmounts">Output amounts in satoshis.</param>
public record GetOutputAmountsResponse(
	long[] OutputAmounts
);
