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
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			Brush.Parse(value switch
			{
				Network network when network == Network.TestNet => "#318522",
				Network network when network == Network.RegTest => "#AE6200",
				_ => "#007ACC"
			});

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
}
