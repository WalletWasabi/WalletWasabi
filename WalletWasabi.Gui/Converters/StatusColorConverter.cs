using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using System;
using System.Globalization;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class StatusColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
			{
				return Brushes.Yellow;
			}

			switch (parameter?.ToString())
			{
				case "BitcoinCoreStatus" when !(value as RpcStatus).Synchronized:
				case "Tor" when Enum.Parse<TorStatus>(value.ToString()) != TorStatus.Running:
				case "Backend" when Enum.Parse<BackendStatus>(value.ToString()) == BackendStatus.NotConnected:
				case "Peers" when (int)value == 0:
				case "FiltersLeft" when value.ToString() != "0":
				case "DownloadingBlock" when value is true:
					return Brushes.Yellow;

				default:
					return ColorTheme.CurrentTheme.Foreground;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
