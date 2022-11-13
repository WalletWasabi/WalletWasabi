namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class NoCoinsToMixException : InvalidOperationException
{
	public NoCoinsToMixException(string? message) : base(message)
	{
	}
}
