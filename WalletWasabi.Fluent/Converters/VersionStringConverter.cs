using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Fluent.Converters
{
	public class VersionStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Version version)
			{
				return version.ToString();
			}
			else
			{
				throw new TypeArgumentException(value, typeof(Version), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Version.Parse(value as string ?? "");
		}
	}
}