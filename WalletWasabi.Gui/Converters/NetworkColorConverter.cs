using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class NetworkColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Network network)
			{
				return network switch
				{
					_ when network == Network.TestNet => Brush.Parse("#AE6200"),
					_ when network == Network.RegTest  => Brush.Parse("#CE6200"),
					_ => Application.Current.Resources["ApplicationAccentBrushLow"] as IBrush
				};
			}
			else
			{
					return Application.Current.Resources["ApplicationAccentBrushLow"] as IBrush;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
