using Avalonia;
using Avalonia.Data.Converters;
using NBitcoin;
using Splat;
using System;
using System.Globalization;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Controls.WalletExplorer;

namespace WalletWasabi.Gui.Converters
{
	public class FeeDisplayFormatConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is TransactionFeeInfo feeInfo)
			{
				FeeDisplayFormat displayFormat = (FeeDisplayFormat)Enum.ToObject(typeof(FeeDisplayFormat), Locator.Current.GetService<Global>().UiConfig.FeeDisplayFormat);

				string feeText;
				string feeToolTip;

				switch (displayFormat)
				{
					case FeeDisplayFormat.SatoshiPerByte:
						feeText = $"(~ {feeInfo.FeeRate.SatoshiPerByte} sat/vbyte)";
						feeToolTip = "Expected fee rate in satoshi / vbyte.";
						break;

					case FeeDisplayFormat.USD:
						feeText = $"(~ ${feeInfo.UsdFee:0.##})";
						feeToolTip = $"Estimated total fees in USD. Exchange Rate: {(long)feeInfo.UsdExchangeRate} BTC/USD.";
						break;

					case FeeDisplayFormat.BTC:
						feeText = $"(~ {feeInfo.EstimatedBtcFee.ToString(false, false)} BTC)";
						feeToolTip = "Estimated total fees in BTC.";
						break;

					case FeeDisplayFormat.Percentage:
						feeText = $"(~ {feeInfo.FeePercentage:0.#} %)";
						feeToolTip = "Expected percentage of fees against the amount to be sent.";
						break;

					default:
						throw new NotSupportedException("This is impossible.");
				}

				return new { FeeText = feeText, FeeToolTip = feeToolTip };
			}
			else
			{
				throw new TypeArgumentException(value, typeof(TransactionFeeInfo), nameof(value));
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
