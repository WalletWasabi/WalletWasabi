namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class InconsistentRoundDataException : InvalidOperationException
{
	public InconsistentRoundDataException(string message) : base(message)
	{
	}
}
