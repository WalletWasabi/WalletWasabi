using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Commands;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Drawing;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class PrivacyLevelValueConverter : IValueConverter
	{
		private static readonly Dictionary<string, DrawingGroup> Cache = new Dictionary<string, DrawingGroup>();

		public DrawingGroup GetIconByName(string icon)
		{
			if (!Cache.TryGetValue(icon, out var image))
			{
				if (Application.Current.Styles.TryGetResource(icon.ToString(), out object resource))
				{
					image = resource as DrawingGroup;
					Cache.Add(icon, image);
				}
				else
				{
					throw new InvalidOperationException($"Icon {icon} not found");
				}
			}

			return image;
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int integer)
			{
				var config = Application.Current.Resources[Global.ConfigResourceKey] as Config;
				string shield;
				if (integer < config.PrivacyLevelSome)
				{
					shield = "Critical";
				}
				else if (integer < config.PrivacyLevelFine)
				{
					shield = "Some";
				}
				else if (integer < config.PrivacyLevelStrong)
				{
					shield = "Fine";
				}
				else if (integer < 9000)
				{
					shield = "Strong";
				}
				else // It's Over 9000!
				{
					shield = "Super";
				}
				var icon = GetIconByName($"Privacy{shield}");
				return new { Icon = icon, ToolTip = $"Anonymity Set: {integer}" };
			}
			else
			{
				throw new TypeArgumentException(value, typeof(int), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
