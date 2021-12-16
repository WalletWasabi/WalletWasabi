using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using Avalonia.Utilities;

namespace WalletWasabi.Fluent.Converters
{
	public class WidthTriggersConverter : IValueConverter
	{
		public static readonly WidthTriggersConverter Instance = new();

		private WidthTriggersConverter()
		{
		}

		object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is AvaloniaList<double> widthTriggers)
			{
				return string.Join(",", widthTriggers.Select(x => x.ToString(CultureInfo.InvariantCulture)));
			}

			return null;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string str)
			{
				var widthTriggers = new AvaloniaList<double>();
				var values = str.Split(',');

				foreach (var s in values)
				{
					if (TypeUtilities.TryConvert(typeof(double), s, culture, out var v))
					{
						widthTriggers.Add((double)v);
					}
					else
					{
						// throw new InvalidCastException($"Could not convert '{s}' to {typeof(double)}.");
						return null;
					}
				}

				return widthTriggers;
			}

			return null;
		}
	}
}