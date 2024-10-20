using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public static class IntConverter
{
	public static readonly IValueConverter ToOrdinalString =
		new FuncValueConverter<int, string>(x => $"{x}.");

	public static readonly IValueConverter IsNullOrZero =
		new FuncValueConverter<int?, bool>(x => x is null or 0);

	public static readonly IValueConverter FPlusConverter =
		new FuncValueConverter<int, string>(x => x > 0 ? "+" + x : x.ToString());
}
