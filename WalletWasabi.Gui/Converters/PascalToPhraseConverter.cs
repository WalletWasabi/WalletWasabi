using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
    public class PascalToPhraseConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var str = value.ToString();
			var pos = 0;
			var builder = new StringBuilder();
			foreach (var c in str)
			{
				if (char.IsUpper(c))
				{
					if (pos > 0)
					{
						builder.Append(" ");
					}

					builder.Append(char.ToLower(c));
				}
				else
				{
					builder.Append(c);
				}
				pos++;
			}
			return builder.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
