namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

public enum Mode
{
	/// <remarks>Corresponds to old <c>isolateStream=false</c>.</remarks>
	DefaultCircuit,

	/// <remarks>Corresponds to old <c>isolateStream=true</c>.</remarks>
	NewCircuitPerRequest,

	SingleCircuitPerLifetime
}
