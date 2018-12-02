using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class PascalToPhraseConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var str = value.ToString();
			var ret = "";
			var pos = 0;
			foreach(var c in str)
			{
				if(char.IsUpper(c))
				{
					if(pos > 0)
						ret += " ";
					ret += char.ToLower(c);
				}
				else
				{
					ret += c;
				}
				pos++;
			}
			return ret;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}