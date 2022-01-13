namespace WalletWasabi.Fluent.Models;

public class RestartNeededEventArgs : EventArgs
{
	public bool IsRestartNeeded { get; init; }
}
