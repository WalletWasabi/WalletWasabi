using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class TimeSpanVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is TimeSpan ts)
			{
				return ts == TimeSpan.Zero ? false : true;
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
