using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class ThicknessConverters
{
	public static readonly IValueConverter Negate =
		new FuncValueConverter<Thickness, Thickness>(thickness =>
			new Thickness(-thickness.Left, -thickness.Top, -thickness.Right, -thickness.Bottom));
}
