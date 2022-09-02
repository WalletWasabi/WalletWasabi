using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class StringConverters
{
	public static readonly IValueConverter AddSIfPluralConverter = new FuncValueConverter<int, string>(TextHelpers.AddSIfPlural);
}
