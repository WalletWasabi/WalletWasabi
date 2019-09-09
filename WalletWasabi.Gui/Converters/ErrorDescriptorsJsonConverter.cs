using Avalonia;
using Avalonia.Data.Converters;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class ErrorDescriptorsJsonConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IEnumerable<Exception> jsonStr)
			{
				return JsonConvert.DeserializeObject<ErrorDescriptors>(jsonStr.First().Message);
			}

			return ErrorDescriptors.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
