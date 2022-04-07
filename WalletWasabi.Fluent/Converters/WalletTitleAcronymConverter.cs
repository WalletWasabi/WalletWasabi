using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class WalletTitleAcronymConverter : IValueConverter
{
	private static readonly Regex AcronymRegex = new(@"((?<=^|\s)(\w{1})|([A-Z]))", RegexOptions.Compiled);

	public static readonly WalletTitleAcronymConverter Instance = new();

	private WalletTitleAcronymConverter()
	{
	}

	object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string title)
		{
			if (title.Length > 8)
			{
				return string.Join(string.Empty, AcronymRegex.Matches(title).Select(x => x.Value));
			}

			return title;
		}

		return AvaloniaProperty.UnsetValue;
	}

	object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}