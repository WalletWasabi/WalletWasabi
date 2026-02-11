namespace WalletWasabi.WabiSabi.Client.Banning;

public record BannedCoordinatorRecord(string CoordinatorUri, DateTimeOffset BannedAt, string Reason);
