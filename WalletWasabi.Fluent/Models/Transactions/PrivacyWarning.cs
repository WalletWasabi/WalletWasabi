using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Models.Transactions;

// TODO: Case Types should be nested inside the base, and remove the "Warning" Suffix
// Avalonia XAML currently does not support {x:Type} references to nested types (https://github.com/AvaloniaUI/Avalonia/issues/2725)
// Revisit this after Avalonia V11 upgrade
public abstract record PrivacyWarning();

public record InterlinksLabelsWarning(LabelsArray Labels) : PrivacyWarning();

public record NonPrivateFundsWarning() : PrivacyWarning();

public record SemiPrivateFundsWarning() : PrivacyWarning();

public record ConsolidationWarning(int CoinCount) : PrivacyWarning();

public record CreatesChangeWarning() : PrivacyWarning();

public record UnconfirmedFundsWarning() : PrivacyWarning();
