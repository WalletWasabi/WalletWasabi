using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AvalonStudio.Commands;
using Splat;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Drawing;
using System.Globalization;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class PrivacyLevelValueConverter : IValueConverter
	{
		private static readonly Dictionary<string, DrawingGroup> Cache = new Dictionary<string, DrawingGroup>();

		public DrawingGroup GetIconByName(string icon)
		{
			if (!Cache.TryGetValue(icon, out var image))
			{
				if (Application.Current.Styles.TryGetResource(icon, out object resource))
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
				var config = Locator.Current.GetService<Global>().Config;
				string shield;
				string toolTip = null;
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
					shield = "Saiyan";
					toolTip = "It's over 9000!!!";
				}

				toolTip ??= $"Anonymity Set: {integer}";
				var icon = GetIconByName($"Privacy{shield}");
				return new { Icon = icon, ToolTip = toolTip };
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
