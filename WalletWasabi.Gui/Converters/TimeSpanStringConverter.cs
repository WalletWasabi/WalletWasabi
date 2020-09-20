using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class TimeSpanStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is TimeSpan ts)
			{
				var builder = new StringBuilder();
				if (ts.Days != 0)
				{
					if (ts.Days == 1)
					{
						builder.Append($"{ts.Days} day ");
					}
					else
					{
						builder.Append($"{ts.Days} days ");
					}
				}
				if (ts.Hours != 0)
				{
					if (ts.Hours == 1)
					{
						builder.Append($"{ts.Hours} hour ");
					}
					else
					{
						builder.Append($"{ts.Hours} hours ");
					}
				}
				if (ts.Minutes != 0)
				{
					if (ts.Minutes == 1)
					{
						builder.Append($"{ts.Minutes} minute ");
					}
					else
					{
						builder.Append($"{ts.Minutes} minutes ");
					}
				}
				if (ts.Seconds != 0)
				{
					if (ts.Seconds == 1)
					{
						builder.Append($"{ts.Seconds} second");
					}
					else
					{
						builder.Append($"{ts.Seconds} seconds");
					}
				}
				return builder.ToString();
			}
			else
			{
				throw new TypeArgumentException(value, typeof(TimeSpan), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
