namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

public interface ICircuit
{
	/// <summary>Name of the circuit.</summary>
	string Name { get; }

	/// <summary>Denotes whether an HTTP(s) request can be sent using the circuit or not anymore.</summary>
	/// <remarks>
	/// Flips only from <c>true</c> to <c>false</c>.
	/// <para>Implementing object must implement the property in a way that it never throws and returns a valid value even after object disposal.</para>
	/// </remarks>
	bool IsActive { get; }
}
