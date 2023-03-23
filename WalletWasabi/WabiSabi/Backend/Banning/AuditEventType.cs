namespace WalletWasabi.WabiSabi.Backend.Banning;

public enum AuditEventType
{
	Exception,
	VerificationResult,
	Round
}

public enum Reason
{
	Remix,
	Whitelisted,
	OneHop,
	RemoteApiChecked,
	Immature,
	Exception
}
