using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Messages;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

/// <summary>
/// An abstraction corresponding with a Tor circuit.
/// </summary>
/// <remarks>
/// Technically, there might be multiple Tor circuits that are used when sending HTTP requests using this <see cref="INamedCircuit"/>.
/// But the important part is that different <see cref="INamedCircuit"/> instances are isolated from each other correctly.
/// </remarks>
public interface INamedCircuit : ICircuit
{
	/// <summary>Name of the circuit.</summary>
	string Name { get; }

	/// <summary>Non-negative number to help us require a new Tor circuit even if <see cref="INamedCircuit.Name">names</see> are the same.</summary>
	/// <remarks>
	/// To request a Tor circuit with required isolation properties, one passes <see cref="UsernamePasswordRequest"/> during Tor SOCKS5 handshake.
	/// <see cref="IsolationId"/> is an integer that is used as <see cref="PasswdField"/> to enforce that we really try to create a new Tor circuit
	/// than in the last attempt.
	/// <para>This property makes sense for circuit types like <see cref="PersonCircuit"/> which can live a long time.</para>
	/// <para>The idea is to increment this property every time we fail to build a Tor circuit with <see cref="ICircuit"/> settings.</para>
	/// </remarks>
	long IsolationId { get; }

	/// <summary>What is the circuit for in human readable form.</summary>
	/// <remarks>For logging purposes only.</remarks>
	string? Purpose { get; }

	/// <summary>Denotes whether an HTTP(s) request can be sent using the circuit or not anymore.</summary>
	/// <remarks>
	/// Flips only from <c>true</c> to <c>false</c>.
	/// <para>Implementing object must implement the property in a way that it never throws and returns a valid value even after object disposal.</para>
	/// </remarks>
	bool IsActive { get; }

	/// <summary>
	/// Mechanism to require a new Tor circuit after we detect HTTP requests' timeouts on the particular Tor circuit or other Tor instabilities of the current Tor circuit.
	/// </summary>
	void IncrementIsolationId();
}
