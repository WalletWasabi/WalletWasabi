using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if(!Enum.TryParse(typeof(CcjRoundPhase), parameter.ToString(), false, out var p))
				throw new ArgumentException($"Unknown '{parameter}' value");

			return ((CcjRoundPhase)value <= (CcjRoundPhase)p) 
				? Brushes.Green
				: Brushes.Gray;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new InvalidOperationException();
		}
	}
}
