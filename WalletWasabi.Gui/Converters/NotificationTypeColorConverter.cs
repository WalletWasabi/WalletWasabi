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
			var background = (parameter.ToString() == "Background");
			IBrush brush = Brush.Parse("#2d2d30");
			switch ((NotificationTypeEnum)value)
			{
				case NotificationTypeEnum.Info: 
					brush = Brush.Parse(background ? "#2d2d30" : "#FFFFFF");
					break;
				case NotificationTypeEnum.Error: 
					brush = Brush.Parse(background ? "#E34937" : "#D1E337");
					break;
				case NotificationTypeEnum.Warning: 
					brush = Brush.Parse(background ? "#D78A04" : "#E0331F");
					break;
				case NotificationTypeEnum.Success: 
					brush = Brush.Parse(background ? "#008800" : "#E4E64C");
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
