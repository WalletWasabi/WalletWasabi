namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public record PayjoinRequest(Uri Endpoint, string Address, decimal Amount);