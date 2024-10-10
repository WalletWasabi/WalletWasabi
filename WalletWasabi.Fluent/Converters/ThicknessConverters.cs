using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class ThicknessConverters
{
	public static readonly IValueConverter Negate =
		new FuncValueConverter<Thickness, Thickness>(thickness =>
			new Thickness(-thickness.Left, -thickness.Top, -thickness.Right, -thickness.Bottom));

	public static readonly IValueConverter ApplyCoinjoinAnonScoreMargins =
		new FuncValueConverter<bool, Thickness>(x => x ? new Thickness(8, 0, 0, 0) : new Thickness(0, 0, 0, 0));

	public static readonly IValueConverter ApplyCoinjoinAmountMargins =
		new FuncValueConverter<bool, Thickness>(x => x ? new Thickness(-52, 0, 0, 0) : new Thickness(-30, 0, 0, 0));
}
