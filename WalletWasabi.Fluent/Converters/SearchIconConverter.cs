using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Fluent.ViewModels.Search;

namespace WalletWasabi.Fluent.Converters
{
	public class SearchIconConverter : IValueConverter
	{
		public static readonly SearchIconConverter Instance = new SearchIconConverter();

		private SearchIconConverter()
		{
		}

		object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is SearchItemViewModel sivm)
			{
				if (Application.Current.Styles.TryGetResource(sivm.IconName, out object? resource))
				{
					return resource;
				}
			}

			return null;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}