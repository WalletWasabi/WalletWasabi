using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class ErrorDescriptorToBorderColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is ErrorDescriptors descriptors)
			{
				return GetColorFromDescriptors(descriptors);
			}
			else if (value is IEnumerable<Exception> exList)
			{
				return GetColorFromDescriptors(ErrorDescriptorsJsonConverter.ExceptionListToErrorDescriptor(exList));
			}

			return null;
		}

		private SolidColorBrush GetColorFromDescriptors(ErrorDescriptors descriptors)
		{
			if (!descriptors.HasErrors) return null;

			var highPrioError = descriptors.OrderByDescending(p => p.Severity).Single();

			if (ErrorSeverityColorConverter.ErrorSeverityColors.TryGetValue(highPrioError.Severity, out var brush))
			{
				return brush;
			}
			else
				return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}