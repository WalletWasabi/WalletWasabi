using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Converters
{
	public class ErrorDescriptorToBorderColorConverter : IValueConverter
	{
		private ErrorDescriptor UndefinedExceptionToErrorDescriptor(Exception ex)
		{
			var newErrDescMessage = ExceptionExtensions.ToTypeMessageString(ex);
			return new ErrorDescriptor(ErrorSeverity.Warning, newErrDescMessage);
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var descriptors = new ErrorDescriptors();

			if (value is IEnumerable<object> rawObj)
			{
				foreach (var error in rawObj.OfType<ErrorDescriptor>())
				{
					descriptors.Add(error);
				}

				foreach (var ex in rawObj.OfType<Exception>())
				{
					descriptors.Add(UndefinedExceptionToErrorDescriptor(ex));
				}
			}
			else if (value is Exception ex)
			{
				descriptors.Add(UndefinedExceptionToErrorDescriptor(ex));
			}
			else
			{
				return null;
			}

			return GetColorFromDescriptors(descriptors);
		}

		private SolidColorBrush GetColorFromDescriptors(ErrorDescriptors descriptors)
		{
			if (!descriptors.HasErrors)
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

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
