using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is RoundPhase phase)
			{
				return phase switch
				{
					RoundPhase.InputRegistration => "Registration",
					RoundPhase.ConnectionConfirmation => "Connection Confirmation",
					RoundPhase.OutputRegistration => "Output Registration",
					RoundPhase.Signing => "Signing",
					_ => ""
				};
			}
			else
			{
				throw new TypeArgumentException(value, typeof(RoundPhase), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var s = value.ToString();

			if (s.Equals("Registration", StringComparison.OrdinalIgnoreCase) || s.Equals(RoundPhase.InputRegistration.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return RoundPhase.InputRegistration;
			}
			else if (s.Equals("Connection Confirmation", StringComparison.OrdinalIgnoreCase) || s.Equals(RoundPhase.ConnectionConfirmation.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return RoundPhase.ConnectionConfirmation;
			}
			else if (s.Equals("Output Registration", StringComparison.OrdinalIgnoreCase) || s.Equals(RoundPhase.OutputRegistration.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return RoundPhase.OutputRegistration;
			}
			else if (s.Equals("Signing", StringComparison.OrdinalIgnoreCase) || s.Equals(RoundPhase.Signing.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return RoundPhase.Signing;
			}

			throw new InvalidOperationException();
		}
	}
}
