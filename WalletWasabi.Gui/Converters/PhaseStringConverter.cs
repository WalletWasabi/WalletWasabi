using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CcjRoundPhase phase)
			{
				switch (phase)
				{
					case CcjRoundPhase.InputRegistration: return "Registration";
					case CcjRoundPhase.ConnectionConfirmation: return "Connection Confirmation";
					case CcjRoundPhase.OutputRegistration: return "Output Registration";
					case CcjRoundPhase.Signing: return "Signing";
					default: return "";
				}
			}
			else
			{
				throw new TypeArgumentException(value, typeof(CcjRoundPhase), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var s = value.ToString();

			if (s.Equals("Registration", StringComparison.OrdinalIgnoreCase) || s.Equals(CcjRoundPhase.InputRegistration.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return CcjRoundPhase.InputRegistration;
			}
			else if (s.Equals("Connection Confirmation", StringComparison.OrdinalIgnoreCase) || s.Equals(CcjRoundPhase.ConnectionConfirmation.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return CcjRoundPhase.ConnectionConfirmation;
			}
			else if (s.Equals("Output Registration", StringComparison.OrdinalIgnoreCase) || s.Equals(CcjRoundPhase.OutputRegistration.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return CcjRoundPhase.OutputRegistration;
			}
			else if (s.Equals("Signing", StringComparison.OrdinalIgnoreCase) || s.Equals(CcjRoundPhase.Signing.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return CcjRoundPhase.Signing;
			}

			throw new InvalidOperationException();
		}
	}
}
