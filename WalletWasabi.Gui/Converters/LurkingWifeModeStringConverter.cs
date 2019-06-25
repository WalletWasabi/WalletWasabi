using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;

namespace WalletWasabi.Gui.Converters
{
	public class LurkingWifeModeStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var uiConfig = Application.Current.Resources[Global.UiConfigResourceKey] as UiConfig;
			if (uiConfig.LurkingWifeMode is true)
			{
				int len = 10;
				if (int.TryParse(parameter.ToString(), out int newLength))
				{
					len = newLength;
				}

				return new string(Enumerable.Repeat('#', len).ToArray());
			}
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
