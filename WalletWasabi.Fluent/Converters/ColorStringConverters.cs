using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace WalletWasabi.Fluent.Converters;

public static class ColorStringConverters
{
	public static readonly IValueConverter HexColorToBrush =
		new FuncValueConverter<string?, IBrush>(x => x switch
		{
			null => Brushes.Magenta,
			{ } => new ImmutableSolidColorBrush(Color.Parse(x))
		});
}
