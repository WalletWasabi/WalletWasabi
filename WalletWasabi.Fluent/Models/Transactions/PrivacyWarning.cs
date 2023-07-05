using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Models.Transactions;

public enum WarningSeverity
{
	Default,
	Warning,
	Info
}

// TODO: Case Types should be nested inside the base, and remove the "Warning" Suffix
// Avalonia XAML currently does not support {x:Type} references to nested types (https://github.com/AvaloniaUI/Avalonia/issues/2725)
// Revisit this after Avalonia V11 upgrade
public abstract record PrivacyWarning(WarningSeverity Severity);

public record InterlinksLabelsWarning(LabelsArray Labels, WarningSeverity Severity = WarningSeverity.Warning) : PrivacyWarning(Severity);

public record NonPrivateFundsWarning(WarningSeverity Severity = WarningSeverity.Warning) : PrivacyWarning(Severity);

public record SemiPrivateFundsWarning(WarningSeverity Severity = WarningSeverity.Warning) : PrivacyWarning(Severity);

public record ConsolidationWarning(int CoinCount, WarningSeverity Severity = WarningSeverity.Warning) : PrivacyWarning(Severity);

public record CreatesChangeWarning(WarningSeverity Severity = WarningSeverity.Info) : PrivacyWarning(Severity);

public record UnconfirmedFundsWarning(WarningSeverity Severity = WarningSeverity.Warning) : PrivacyWarning(Severity);
