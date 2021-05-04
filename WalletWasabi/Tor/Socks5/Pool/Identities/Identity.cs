namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	public interface IIdentity
	{
		string Name { get; }

		bool CanTorCircuitBeReused { get; }
	}
}
