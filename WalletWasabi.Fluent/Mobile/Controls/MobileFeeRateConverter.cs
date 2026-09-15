using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using NBitcoin;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>Unknown estimates stay unknown; never dereference a not-yet-bound fee rate.</summary>
public sealed class MobileFeeRateConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is FeeRate rate
			? $"Fee rate: {rate.SatoshiPerByte.ToString("0.###", CultureInfo.InvariantCulture)} sat/vB"
			: "Fee rate unavailable";

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
