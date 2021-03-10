using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Converters
{
	public class NavBarItemsListBoxAlignmentConverter : IValueConverter
	{
		public static readonly NavBarItemsListBoxAlignmentConverter Instance = new();

		private NavBarItemsListBoxAlignmentConverter()
		{
		}

		object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int count)
			{
				return count == 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
			}

			return null;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}