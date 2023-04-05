namespace WalletWasabi.Exceptions;

public class TurboSyncDoubleTxProcessException : Exception
{
	public TurboSyncDoubleTxProcessException()
		: base("A tx would've been processed twice because of TurboSync, stop it and perform a full sync.")
	{
	}
}
