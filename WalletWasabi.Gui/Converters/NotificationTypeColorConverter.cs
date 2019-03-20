using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using WalletWasabi.Models;
using WalletWasabi.Gui.ViewModels;
using Avalonia.Animation;
using Avalonia.Styling;

namespace WalletWasabi.Gui.Converters
{
	public class NotificationTypeColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			IBrush brush = Brush.Parse("#2d2d30");
			switch ((NotificationTypeEnum)value)
			{
				case NotificationTypeEnum.Info: 
					brush = Brush.Parse("#2d2d30");
					break;
				case NotificationTypeEnum.Error: 
					brush = Brush.Parse("#E34937");
					break;
				case NotificationTypeEnum.Warning: 
					brush = Brush.Parse("#D78A04");
					break;
				case NotificationTypeEnum.Success: 
					brush = Brush.Parse("#008800");
					break;
			}

			return brush;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
