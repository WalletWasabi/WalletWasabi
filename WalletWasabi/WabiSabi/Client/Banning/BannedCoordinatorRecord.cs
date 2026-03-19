namespace WalletWasabi.WabiSabi.Client.Banning;

public record BannedCoordinatorRecord(string CoordinatorHost, DateTimeOffset BannedAt, string Reason);
