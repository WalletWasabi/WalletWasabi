using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

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