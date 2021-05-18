using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Utilities;

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