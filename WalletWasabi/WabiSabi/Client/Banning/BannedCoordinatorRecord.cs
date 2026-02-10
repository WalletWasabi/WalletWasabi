namespace WalletWasabi.WabiSabi.Client.Banning;

public record BannedCoordinatorRecord
{
	public BannedCoordinatorRecord(string coordinatorUri, DateTimeOffset bannedAt, string reason)
	{
		CoordinatorUri = coordinatorUri;
		BannedAt = bannedAt;
		Reason = reason;
	}

	public string CoordinatorUri { get; set; }
	public DateTimeOffset BannedAt { get; set; }
	public string Reason { get; set; }
}
