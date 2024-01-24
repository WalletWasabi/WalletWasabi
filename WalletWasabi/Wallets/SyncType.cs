namespace WalletWasabi.Wallets;

public enum SyncType
{
	/// <summary>
	/// Test all external keys + internal with coins on them at the height of the filter.
	/// </summary>
	Turbo,
	
	/// <summary>
	/// Test all the non-Turbo keys (internal that already spent their coins at the height of the filter).
	/// </summary>
	NonTurbo,
	
	/// <summary>
	/// Test all the keys.
	/// </summary>
	Complete
}
