using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Models.Transactions;

namespace WalletWasabi.Fluent.Mobile.Controls;

public sealed class MobilePrivacyMessageConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
	{
		InterlinksLabelsWarning warning => $"This transaction links these labels: {warning.Labels}.",
		TransactionKnownAsYoursByWarning warning => $"This transaction may be recognized by: {warning.Labels}.",
		NonPrivateFundsWarning => "This transaction spends non-private coins.",
		SemiPrivateFundsWarning => "Some selected coins do not yet meet your anonymity target.",
		LargePercentSpentWarning warning => $"This transaction spends {warning.PercentSpent}% of your balance. Matching amounts may reveal earlier inputs.",
		CreatesChangeWarning => "This transaction creates change. Later spending may link it to this payment.",
		UnconfirmedFundsWarning => "This transaction spends unconfirmed coins and may be delayed or rejected.",
		CoinjoiningFundsWarning => "This transaction spends coins currently in CoinJoin. Consider waiting for the round to finish.",
		PrivacyWarning => "Review this transaction's privacy before signing.",
		LabelManagementSuggestion => "Choose which sender labels this transaction can link.",
		FullPrivacySuggestion => "Use the suggested amount to improve privacy.",
		BetterPrivacySuggestion => "Use the suggested amount for better coin selection.",
		ChangeAvoidanceSuggestion => "Adjust the payment amount to avoid a change output.",
		_ => "Review the available privacy suggestion."
	};
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Avalonia.Data.BindingOperations.DoNothing;
}
