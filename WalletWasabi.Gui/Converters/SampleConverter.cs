using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	/// <summary>
	/// <para>
	/// Converts a string path to a bitmap asset.
	/// </para>
	/// <para>
	/// The asset must be in the same assembly as the program. If it isn't,
	/// specify "avares://<assemblynamehere>/" in front of the path to the asset.
	/// </para>
	/// </summary>
	public class SampleConverter : IValueConverter
	{
		public static SampleConverter Instance = new SampleConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return null;
			}

			if (value is string rawUri && targetType == typeof(IBitmap))
			{
				Uri uri;

				// Allow for assembly overrides
				if (rawUri.StartsWith("avares://"))
				{
					uri = new Uri(rawUri);
				}
				else
				{
					string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
					uri = new Uri($"avares://{assemblyName}{rawUri}");
				}

				var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
				var asset = assets.Open(uri);

				return new Bitmap(asset);
			}

			throw new NotSupportedException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
