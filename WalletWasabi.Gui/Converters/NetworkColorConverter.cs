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
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (Application.Current.TryFindResource(value.ToString(), out var color))
			{
				return color;
			}

			return "#007ACC";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
}
