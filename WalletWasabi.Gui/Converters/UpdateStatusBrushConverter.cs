using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Converters
{
	public class UpdateStatusBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is UpdateStatus status)
			{
				switch (status)
				{
					case UpdateStatus.Critical:
						return Brushes.IndianRed;
				}
			}

			if (Application.Current.Resources.TryGetResource("ApplicationAccentBrushLow", out object brush))
			{
				return brush;
			}

			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
