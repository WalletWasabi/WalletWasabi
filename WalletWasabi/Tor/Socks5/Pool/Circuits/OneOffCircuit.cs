using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

/// <summary>
/// Random Tor circuit for an HTTP request which should not be linked with any other HTTP request sent previously.
/// </summary>
public class OneOffCircuit : ICircuit
{
	public OneOffCircuit()
	{
		Name = RandomString.CapitalAlphaNumeric(21);
	}

	public string Name { get; }

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"[{nameof(OneOffCircuit)}: {Name}]";
	}
}
