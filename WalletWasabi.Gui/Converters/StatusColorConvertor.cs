using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class StatusColorConvertor : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool isTor = Enum.TryParse(value.ToString(), out TorStatus tor);
			if (isTor && tor == TorStatus.NotRunning)
			{
				return Brushes.Yellow;
			}

			bool isBackend = Enum.TryParse(value.ToString(), out BackendStatus backend);
			if (isBackend && backend == BackendStatus.NotConnected)
			{
				return Brushes.Yellow;
			}

			return ColorTheme.CurrentTheme.Foreground;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
