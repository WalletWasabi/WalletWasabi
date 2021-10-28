using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Converters
{
	public class ErrorDescriptorToBorderColorConverter : IValueConverter
	{
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var descriptors = ErrorDescriptors.Create();

			if (value is IEnumerable<object> rawObj)
			{
				foreach (var error in rawObj.OfType<ErrorDescriptor>())
				{
					descriptors.Add(error);
				}
			}
			else
			{
				return null;
			}

			return GetColorFromDescriptors(descriptors);
		}

		private SolidColorBrush? GetColorFromDescriptors(ErrorDescriptors descriptors)
		{
			if (!descriptors.Any())
			{
				return null;
			}

			var highPrioError = descriptors.OrderByDescending(p => p.Severity).FirstOrDefault();

			if (ErrorSeverityColorConverter.ErrorSeverityColors.TryGetValue(highPrioError.Severity, out var brush))
			{
				return brush;
			}
			else
			{
				return null;
			}
		}

		public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
