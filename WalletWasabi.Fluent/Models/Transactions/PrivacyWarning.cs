using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Models.Transactions;

public enum WarningSeverity
{
	Default,
	Critical,
	Warning,
	Info
}

// TODO: Case Types should be nested inside the base, and remove the "Warning" Suffix
// Avalonia XAML currently does not support {x:Type} references to nested types (https://github.com/AvaloniaUI/Avalonia/issues/2725)
// Revisit this after Avalonia V11 upgrade
public abstract record PrivacyWarning(WarningSeverity Severity) : PrivacyItem();

public record InterlinksLabelsWarning(LabelsArray Labels) : PrivacyWarning(WarningSeverity.Critical);

public record TransactionKnownAsYoursByWarning(LabelsArray Labels) : PrivacyWarning(WarningSeverity.Critical);

public record NonPrivateFundsWarning() : PrivacyWarning(WarningSeverity.Critical);

public record SemiPrivateFundsWarning() : PrivacyWarning(WarningSeverity.Warning);

public record ConsolidationWarning(int CoinCount) : PrivacyWarning(WarningSeverity.Warning);

public record CreatesChangeWarning() : PrivacyWarning(WarningSeverity.Info);

public record UnconfirmedFundsWarning() : PrivacyWarning(WarningSeverity.Info);

public record CoinjoiningFundsWarning() : PrivacyWarning(WarningSeverity.Info);
