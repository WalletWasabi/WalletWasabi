using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Converters
{
	public class NavBarActionsOrientationConverter : IValueConverter
	{
		public static readonly NavBarActionsOrientationConverter Instance = new();

		private NavBarActionsOrientationConverter()
		{
		}

		object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool hideItems)
			{
				return hideItems ? Orientation.Vertical : Orientation.Horizontal;
			}

			return null;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}