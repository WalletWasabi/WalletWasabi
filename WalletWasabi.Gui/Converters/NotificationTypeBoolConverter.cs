using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Converters
{
	public class NotificationTypeBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((NotificationTypeEnum)value == NotificationTypeEnum.None)
				? 0.0
				: 1.0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((double)value <= 0.0001) ? NotificationTypeEnum.None : NotificationTypeEnum.Info;
		}
	}
}
