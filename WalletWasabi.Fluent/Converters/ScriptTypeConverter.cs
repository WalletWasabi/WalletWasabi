using System.Globalization;
using Avalonia.Data.Converters;
using NBitcoin;

namespace WalletWasabi.Fluent.Converters;

public class ScriptTypeConverter : IValueConverter
{
	public static readonly ScriptTypeConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value switch
		{
			ScriptType.MultiSig => "",
			null => "",
			ScriptType.Witness => "",
			ScriptType.P2SH => "",
			ScriptType.P2PK => "",
			ScriptType.P2PKH => "",
			ScriptType.P2WSH => "SW",
			ScriptType.P2WPKH => "SW",
			ScriptType.Taproot => "TR",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
		};
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
