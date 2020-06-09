using Avalonia;
using Avalonia.Data.Converters;
using NBitcoin;
using Splat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class LurkingWifeModeStringConverter : IValueConverter
	{
		public static UiConfig UiConfig { get; private set; }

		public static void InjectDependencies(UiConfig uiConfig)
		{
			UiConfig = uiConfig;
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (UiConfig.LurkingWifeMode)
			{
				int len = 10;
				if (int.TryParse(parameter.ToString(), out int newLength))
				{
					len = newLength;
				}

				return new string(Enumerable.Repeat('#', len).ToArray());
			}
			else if (value is Money)
			{
				var conv = new MoneyStringConverter();
				return conv.Convert(value, targetType, parameter, culture);
			}
			else
			{
				return value?.ToString() ?? "";
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value?.ToString() ?? "";
		}
	}
}
