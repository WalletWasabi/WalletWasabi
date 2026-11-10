using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Client.Wallets.Exchange;

namespace WalletWasabi.Fluent.Converters;

public class CurrencyCodeToDisplayNameConverter : IValueConverter
{
	public static readonly CurrencyCodeToDisplayNameConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is string code && ExchangeRateProviders.CurrencyNames.TryGetValue(code, out var name)
			? $"{code} ({name})"
			: value;

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
