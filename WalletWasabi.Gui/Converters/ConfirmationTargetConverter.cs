using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace WalletWasabi.Gui.Converters
{
	public class ConfirmationTargetConverter : IValueConverter
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
				var config = Application.Current.Resources[Global.ConfigResourceKey] as Config;
				string iconName;
				string toolTip = $"{integer} Confirmations";

				if (integer >= config.ConfirmationTarget)
				{
					iconName = "ConfTargetConfirmed";
				}
				else if (integer > 0)
				{
					iconName = "ConfTargetWaiting";
				}
				else
				{
					iconName = "ConfTargetUnconfirmed";
					toolTip = "Unconfirmed";
				}
				
				return new {Icon = GetIconByName(iconName), ToolTip = toolTip};
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
