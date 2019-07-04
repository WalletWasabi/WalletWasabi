using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class BooleanStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is bool on))
			{
				throw new TypeArgumentException(value, typeof(bool), nameof(value));
			}
			if (!(parameter is string str))
			{
				throw new TypeArgumentException(parameter, typeof(string), nameof(parameter));
			}

			var options = str.Split(':');
			if (options.Length < 2)
			{
				throw new ArgumentException("Two options are required by the converter.", nameof(parameter));
			}
			
			return on ? options[0] : options[1];
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}

namespace WalletWasabi.Gui.Converters
{
	public static class BooleanConverters
	{
		public static readonly IValueConverter Not =
			new FuncValueConverter<bool, bool>(x => !x);

		public static IValueConverter Stringify(string trueString, string falseString)
			=> new FuncValueConverter<bool, string>(x => x ? trueString : falseString);

		public static readonly IValueConverter OnOff = Stringify("On", "Off");
		public static readonly IValueConverter ClearMax = Stringify("Clear", "Max");
		public static readonly IValueConverter HideShow = Stringify("Hide", "Show");
	}
}