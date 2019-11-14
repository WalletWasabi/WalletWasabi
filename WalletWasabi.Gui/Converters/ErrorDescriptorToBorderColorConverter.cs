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
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IEnumerable<object> rawObj)
			{
				var descriptors = new ErrorDescriptors();

				foreach (var obj in rawObj)
				{
					switch (obj)
					{
						case ErrorDescriptor ed:
							descriptors.Add(ed);
							break;
						case Exception ex:
							Logger.LogError(ex);
							break;
					}
				}

				return GetColorFromDescriptors(descriptors);
			}

			return null;
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
