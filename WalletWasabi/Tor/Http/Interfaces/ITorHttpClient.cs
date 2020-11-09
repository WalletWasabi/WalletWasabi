namespace WalletWasabi.Tor.Http.Interfaces
{
	public interface ITorHttpClient : IRelativeHttpClient
	{
		bool IsTorUsed { get; }
	}
}
