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
public abstract record PrivacySuggestion(BuildTransactionResult? Transaction) : PrivacyItem();

public record LabelManagementSuggestion(BuildTransactionResult? Transaction = null, LabelsArray? NewLabels = null) : PrivacySuggestion(Transaction);

public record FullPrivacySuggestion(BuildTransactionResult Transaction, decimal Difference, string DifferenceFiatText, IEnumerable<SmartCoin> Coins, bool IsChangeless) : PrivacySuggestion(Transaction);

public record BetterPrivacySuggestion(BuildTransactionResult Transaction, string DifferenceFiatText, IEnumerable<SmartCoin> Coins, bool IsChangeless) : PrivacySuggestion(Transaction);

public record ChangeAvoidanceSuggestion(BuildTransactionResult Transaction, decimal Difference, string DifferenceFiatText, bool IsMore, bool IsLess) : PrivacySuggestion(Transaction)
{
	public Money GetAmount(BitcoinAddress destination) => Transaction!.CalculateDestinationAmount(destination);
}
