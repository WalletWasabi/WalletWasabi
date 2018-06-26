using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class PrivacyLevelValueConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int integer)
			{
				if (integer <= 0)
				{
					return "None";
				}
				else if (integer >= 1 && integer <= 6)
				{
					return "Some";
				}
				else if (integer >= 7 && integer <= 49)
				{
					return "Fine";
				}
				else if (integer > 49)
				{
					return "Strong";
				}
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
