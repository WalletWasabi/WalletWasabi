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
			var config = Application.Current.Resources[Global.ConfigResourceKey] as Config;
			var uiConfig = Application.Current.Resources[Global.UiConfigResourceKey] as UiConfig;
			if (value is int integer)
			{
				// If value is negative, LurkingWifeMode should not be taken into account
				string shield;
				if (uiConfig.LurkingWifeMode is true && integer > 0)
				{
					shield = "Hide";
				}
				else if (Math.Abs(integer) < config.PrivacyLevelSome)
				{
					shield = "Critical";
				}
				else if (Math.Abs(integer) < config.PrivacyLevelFine)
				{
					shield = "Some";
				}
				else if (Math.Abs(integer) < config.PrivacyLevelStrong)
				{
					shield = "Fine";
				}
				else
				{
					shield = "Strong";
				}
				var icon = GetIconByName($"Privacy{shield}");
				return new { Icon = icon, ToolTip = $"Anonymity Set: {Math.Abs(integer)}" };
			}

			throw new InvalidOperationException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
