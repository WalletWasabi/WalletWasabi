using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Converters;

public static class TimespanConverters
{
	public static readonly IValueConverter ToFriendlyString = new FuncValueConverter<TimeSpan, string>(TextHelpers.TimeSpanToFriendlyString);
}
