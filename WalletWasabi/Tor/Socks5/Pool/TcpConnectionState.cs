namespace WalletWasabi.Tor.Socks5.Pool;

public enum TcpConnectionState
{
	/// <summary><see cref="TorTcpConnection"/> is in use currently.</summary>
	InUse,

	/// <summary><see cref="TorTcpConnection"/> can be used for a new HTTP request.</summary>
	FreeToUse,

	/// <summary><see cref="TorTcpConnection"/> is to be disposed.</summary>
	ToDispose
}
