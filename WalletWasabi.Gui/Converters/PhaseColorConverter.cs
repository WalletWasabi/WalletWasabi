using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Gui.Models;

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

			var phaseState = (RoundPhaseState)value;

			return (RoundPhase)p <= phaseState.Phase
				? phaseState.Error
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
