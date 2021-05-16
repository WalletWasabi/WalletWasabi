using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters
{
	public class TilesPositionConverter : IValueConverter
	{
		public static readonly TilesPositionConverter Instance = new();

		private TilesPositionConverter()
		{
		}

		object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int layoutIndex)
			{
				switch (layoutIndex)
				{
					default:
					case 0:
						return Avalonia.Controls.Dock.Top;
					case 1:
						return Avalonia.Controls.Dock.Top;
					case 2:
						return Avalonia.Controls.Dock.Left;
				}
			}

			return null;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}