using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Extensibility.Theme;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Converters
{
	public class BlockDownloadingStatusColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var status = (BlockDownloadingStatus)value;
			switch (status)
			{
				case BlockDownloadingStatus.None: 
					return Brushes.Gray;
				case BlockDownloadingStatus.DownloadedFromLocal:
					return Brushes.Green;
				case BlockDownloadingStatus.DownloadedFromRemote:
					return Brushes.GreenYellow;
				case BlockDownloadingStatus.NoDownloadedRestricted:
					return Brushes.Red;
				default:
					return Brushes.Black;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class BlockDownloadingStatusTipConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var status = (BlockDownloadingStatus)value;
			switch (status)
			{
				case BlockDownloadingStatus.None: 
					return "No block was requested yet.";
				case BlockDownloadingStatus.DownloadedFromLocal:
					return "Latest block was downloaded from local node.";
				case BlockDownloadingStatus.DownloadedFromRemote:
					return "Latest block was downloaded from a remote node.";
				case BlockDownloadingStatus.NoDownloadedRestricted:
					return "No block downloaded. Cannot connect to local node.";
				default:
					return "";
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

}
