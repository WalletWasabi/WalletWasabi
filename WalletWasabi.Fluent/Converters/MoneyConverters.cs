using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Converters;

public static class MoneyConverters
{
	public static readonly IValueConverter ToUsd =
		new FuncValueConverter<decimal, string>(n => n.ToUsd());

	public static readonly IValueConverter ToUsdAproxBetweenParens =
		new FuncValueConverter<decimal, string>(n => n.ToUsdAproxBetweenParens());

	public static readonly IValueConverter ToBtc =
		new FuncValueConverter<Money, string>(n => n?.ToDecimal(MoneyUnit.BTC).FormattedBtc() + " BTC");
}
