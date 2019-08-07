using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class BooleanStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool on)
			{
				if (parameter is string str)
				{
					var options = str.Split(':');
					if (options.Length < 2)
					{
						throw new ArgumentException("Two options are required by the converter.", nameof(parameter));
					}

					return on ? options[0] : options[1];
				}
				else
				{
					throw new TypeArgumentException(parameter, typeof(string), nameof(parameter));
				}
			}
			else
			{
				throw new TypeArgumentException(value, typeof(bool), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string val)
			{
				if (parameter is string param)
				{
					var options = param.Split(':');
					if (options.Length < 2)
					{
						throw new ArgumentException("Two options are required by the converter.", nameof(parameter));
					}

					if (options[0] == val)
					{
						return true;
					}
					else if (options[1] == val)
					{
						return false;
					}

					throw new ArgumentException("Value not found in the given options.", nameof(value));
				}
				else
				{
					throw new TypeArgumentException(parameter, typeof(string), nameof(parameter));
				}
			}
			else
			{
				throw new TypeArgumentException(value, typeof(string), nameof(value));
			}
		}
	}
}
