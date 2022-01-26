using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Socks5.Exceptions;

/// <summary>
/// Exception thrown when a disposed <see cref="ICircuit"/> is used to send an HTTP request.
/// </summary>
public class TorCircuitExpiredException : TorException
{
	public TorCircuitExpiredException() : base()
	{
	}

	public TorCircuitExpiredException(string message) : base(message)
	{
	}

	public TorCircuitExpiredException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
