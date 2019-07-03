using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Converters
{
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
