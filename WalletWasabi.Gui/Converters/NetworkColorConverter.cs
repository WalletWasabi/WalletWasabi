using Avalonia;
using Avalonia.Data.Converters;
using NBitcoin;
using System;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class NetworkColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value switch
			{
				Network network => Application.Current.Resources[network.Name],
				_ => throw new NotSupportedException()
			};

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
}
