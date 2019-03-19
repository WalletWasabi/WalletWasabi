using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using WalletWasabi.Models;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Converters
{
	public class NotificationTypeColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			switch ((NotificationTypeEnum)value)
			{
				case NotificationTypeEnum.None: 
					return ColorTheme.CurrentTheme.Foreground;
				case NotificationTypeEnum.Error: 
					return ColorTheme.CurrentTheme.ErrorListError;
				case NotificationTypeEnum.Warning: 
					return ColorTheme.CurrentTheme.ErrorListWarning;
				case NotificationTypeEnum.Info: 
					return Brush.Parse("#008800");
			}
			return ColorTheme.CurrentTheme.Background;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
