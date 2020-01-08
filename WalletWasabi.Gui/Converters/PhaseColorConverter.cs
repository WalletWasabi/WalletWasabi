using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Splat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.CoinJoin.Common.Models;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!Enum.TryParse(typeof(RoundPhase), parameter.ToString(), false, out var p))
			{
				throw new ArgumentException($"Unknown '{parameter}' value");
			}

			var global = Locator.Current.GetService<Global>();
			var phaseError = global.ChaumianClient.State.IsInErrorState;

			return (RoundPhase)p <= (RoundPhase)value
				? phaseError
					? Brushes.IndianRed
					: Brushes.Green
				: Brushes.Gray;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new InvalidOperationException();
		}
	}
}
