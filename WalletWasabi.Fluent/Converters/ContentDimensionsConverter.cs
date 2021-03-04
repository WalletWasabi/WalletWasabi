using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Policy;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Converters
{
	public class ContentDimensionsConverter : IMultiValueConverter
	{
		public static readonly IMultiValueConverter Instance = new ContentDimensionsConverter();

		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Count != 3)
			{
				return 0;
			}

			if (values[0] is bool hasCustomSize &&
			    values[1] is double defaultSize1 &&
			    values[2] is double customSize)
			{
				return hasCustomSize ? customSize : defaultSize1;
			}

			if (values[1] is double defaultSize2)
			{
				return defaultSize2;
			}

			return 0;
		}
	}
}