using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public static class ListCountConverters
{
	public static readonly IValueConverter IsSingleItem =
		new FuncValueConverter<int, bool>(x => x == 1);
}