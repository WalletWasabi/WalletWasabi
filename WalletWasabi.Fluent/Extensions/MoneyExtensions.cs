using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Extensions;

public static class MoneyExtensions
{
	public static string ToUsd(this decimal n) => n.RoundToSignificantFigures(5) + " USD";
	public static string ToUsdAprox(this decimal n) => $"(≈{ToUsd(n)})";
	public static decimal BtcToUsd(this Money money, decimal exchangeRate) => money.ToDecimal(MoneyUnit.BTC) * exchangeRate;
}
