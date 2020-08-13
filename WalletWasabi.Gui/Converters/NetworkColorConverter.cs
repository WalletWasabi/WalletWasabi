using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class NetworkColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value switch
			{
				Network network => Application.Current.Resources[network.Name],
				_ => Application.Current.Resources[Network.Main.Name]
			};

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
}
