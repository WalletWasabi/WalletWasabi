using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!Enum.TryParse(typeof(CcjRoundPhase), parameter.ToString(), false, out var p))
			{
				throw new ArgumentException($"Unknown '{parameter}' value");
			}

			var phaseError = Global.Instance.ChaumianClient.State.IsInErrorState;

			return ((CcjRoundPhase)p <= (CcjRoundPhase)value)
				? (phaseError ? Brushes.IndianRed : Brushes.Green)
				: Brushes.Gray;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new InvalidOperationException();
		}
	}
}
