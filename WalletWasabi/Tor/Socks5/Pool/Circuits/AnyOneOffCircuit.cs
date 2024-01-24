namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

/// <summary>
/// Special type representing any <see cref="OneOffCircuit"/> that is ready to send an HTTP request.
/// </summary>
public class AnyOneOffCircuit : ICircuit
{
	public static readonly AnyOneOffCircuit Instance = new();

	private AnyOneOffCircuit()
	{
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"[{nameof(AnyOneOffCircuit)}]";
	}
}
