using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalletWasabi.CoinJoin;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Gui.Converters
{
	public class PhaseStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Phase phase)
			{
				return phase switch
				{
					Phase.InputRegistration => "Registration",
					Phase.ConnectionConfirmation => "Connection Confirmation",
					Phase.OutputRegistration => "Output Registration",
					Phase.Signing => "Signing",
					_ => ""
				};
			}
			else
			{
				throw new TypeArgumentException(value, typeof(Phase), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var s = value.ToString();

			if (s.Equals("Registration", StringComparison.OrdinalIgnoreCase) || s.Equals(Phase.InputRegistration.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return Phase.InputRegistration;
			}
			else if (s.Equals("Connection Confirmation", StringComparison.OrdinalIgnoreCase) || s.Equals(Phase.ConnectionConfirmation.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return Phase.ConnectionConfirmation;
			}
			else if (s.Equals("Output Registration", StringComparison.OrdinalIgnoreCase) || s.Equals(Phase.OutputRegistration.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return Phase.OutputRegistration;
			}
			else if (s.Equals("Signing", StringComparison.OrdinalIgnoreCase) || s.Equals(Phase.Signing.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return Phase.Signing;
			}

			throw new InvalidOperationException();
		}
	}
}
