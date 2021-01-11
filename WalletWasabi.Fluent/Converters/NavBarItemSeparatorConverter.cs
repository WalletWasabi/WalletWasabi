using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.Converters
{
	public class NavBarItemSeparatorConverter : IValueConverter
	{
		public static readonly NavBarItemSeparatorConverter Instance = new NavBarItemSeparatorConverter();

		private NavBarItemSeparatorConverter()
		{
		}

		object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is NavBarItemSelectionMode mode)
			{
				return mode != NavBarItemSelectionMode.None;
			}

			return null;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}