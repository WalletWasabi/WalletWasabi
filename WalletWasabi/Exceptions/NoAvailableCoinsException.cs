namespace WalletWasabi.Exceptions;

public class NoAvailableCoinsException : Exception
{
	public NoAvailableCoinsException() : base("No Available Coins at the moment. Try again after the coinjoin.")
	{
	}
}
