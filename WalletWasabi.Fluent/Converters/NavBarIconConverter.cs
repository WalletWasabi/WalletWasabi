using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Converters
{
	public class NavBarIconConverter : IValueConverter
	{
		public static readonly NavBarIconConverter Instance = new NavBarIconConverter();

		private NavBarIconConverter()
		{
		}

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is NavBarItemViewModel nvivm)
			{
				if (Application.Current.Styles.TryGetResource(nvivm.IconName, out object resource))
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
