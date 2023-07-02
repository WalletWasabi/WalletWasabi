using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Models.Transactions;

// TODO: Case Types should be nested inside the base, and remove the "Suggestion" Suffix
// Avalonia XAML currently does not support {x:Type} references to nested types (https://github.com/AvaloniaUI/Avalonia/issues/2725)
// Revisit this after Avalonia V11 upgrade
public abstract record PrivacySuggestion(BuildTransactionResult? Transaction);

public record LabelManagementSuggestion(BuildTransactionResult? Transaction = null, LabelsArray? NewLabels = null) : PrivacySuggestion(Transaction);

public record FullPrivacySuggestion(BuildTransactionResult Transaction, /*Money Difference, IEnumerable<SmartCoin> CoinsToRemove,*/ string DifferenceFiatText) : PrivacySuggestion(Transaction);

public record BetterPrivacySuggestion(BuildTransactionResult Transaction, /*Money Difference, IEnumerable<SmartCoin> CoinsToRemove,*/ string DifferenceFiatText) : PrivacySuggestion(Transaction);

public record ChangeAvoidanceSuggestion(BuildTransactionResult Transaction, string DifferenceFiatText) : PrivacySuggestion(Transaction)
{
	public Money GetAmount() => Transaction!.CalculateDestinationAmount();
}
