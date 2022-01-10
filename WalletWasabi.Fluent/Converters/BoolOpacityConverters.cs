using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public static class BoolOpacityConverters
{
	public static readonly IValueConverter BoolToOpacity =
		new FuncValueConverter<bool, double>(x => x ? 1.0 : 0.0);

	public static readonly IValueConverter OpacityToBool =
		new FuncValueConverter<double, bool>(x => x > 0);
}
