using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class ErrorSeverityColorConverter : IValueConverter
	{
		internal static Dictionary<ErrorSeverity, SolidColorBrush> ErrorSeverityColors = new Dictionary<ErrorSeverity, SolidColorBrush>
		{
			{ ErrorSeverity.Default, new SolidColorBrush(Colors.White) },
			{ ErrorSeverity.Info, new SolidColorBrush(Colors.LightCyan) },
			{ ErrorSeverity.Warning, new SolidColorBrush(Colors.Gold) },
			{ ErrorSeverity.Error, new SolidColorBrush(Colors.IndianRed) }
		};

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is ErrorSeverity severity)
			{
				if (ErrorSeverityColors.TryGetValue(severity, out var brush))
				{
					return brush;
				}
			}
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
