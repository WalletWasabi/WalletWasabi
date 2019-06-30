using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CcjRoundPhase phase)
			{
				if (phase == CcjRoundPhase.InputRegistration)
				{
					return "Registration";
				}
				else if (phase == CcjRoundPhase.ConnectionConfirmation)
				{
					return "Connection Confirmation";
				}
				else if (phase == CcjRoundPhase.OutputRegistration)
				{
					return "Output Registration";
				}
				else if (phase == CcjRoundPhase.Signing)
				{
					return "Signing";
				}

				throw new InvalidOperationException();
			}

			throw new InvalidOperationException();
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
