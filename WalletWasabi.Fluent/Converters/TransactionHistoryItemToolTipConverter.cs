using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Converters;

/// <summary>
/// TODO: This converter is needed because Avalonia doesn't behave correctly when bindings are set dynamically using Styles.
/// This should be fixed in the future. Whenever that happens, reverting the commit in which this file is added should be enough.
/// More info: This is needed because the confirmation tooltip didn't keep up to date even though the DataContext had the correct strings.
/// If this is removed and the commit is reverted eventually, please, check that confirmation count displays the correct text in the Transaction History, especially, after some minutes.
/// </summary>
public class TransactionHistoryItemToolTipConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is HistoryItemViewModelBase vm)
		{
			if (vm.Transaction.Status == TransactionStatus.Confirmed)
			{
				return vm.Transaction.ConfirmedTooltip;
			}

			if (vm.Transaction.TransactionSummary.TryGetConfirmationTime(out var estimate))
			{
				var friendlyString = TextHelpers.TimeSpanToFriendlyString(estimate.Value);
				if (friendlyString != "")
				{
					if (vm.Transaction.Status == TransactionStatus.SpeedUp)
					{
						return $"Pending (accelerated, confirming in ≈ {friendlyString})";
					}

					if (vm.Transaction.Status == TransactionStatus.Pending)
					{
						return $"Pending (confirming in ≈ {friendlyString})";
					}
				}
			}

			if (vm.Transaction.Status == TransactionStatus.SpeedUp)
			{
				return "Pending (accelerated)";
			}

			if (vm.Transaction.Status == TransactionStatus.Pending)
			{
				return "Pending";
			}
		}

		return BindingNotification.UnsetValue;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException("This converter does not support ConvertBack");
	}
}
