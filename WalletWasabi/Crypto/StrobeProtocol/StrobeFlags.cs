namespace WalletWasabi.Crypto.StrobeProtocol;

/// <summary>
/// The behavior of each of Strobe's operations is defined completely by 6 features, called flags.
/// </summary>
[Flags]
public enum StrobeFlags : byte
{
	/// <summary>
	/// Inbound.
	/// If set, this flag means that the operation moves data from the transport, to the cipher,
	/// to the application. An operation without the I flag set is said to be Outbound.
	/// The I flag is clear on all send operations, and set on all recv operations.
	/// </summary>
	I = 1,

	/// <summary>
	/// Application.
	/// If set, this flag means that the operation has data coming to or from the application side.
	/// An operation with I and A both set outputs bytes to the application.
	/// An operation with A set but I clear takes input from the application.
	/// </summary>
	A = 2,

	/// <summary>
	/// Cipher.
	/// If set, this flag means that the operation's output depends cryptographically on the Strobe cipher state.
	/// For operations which don't have I or T flags set, neither party produces output with this operation.
	/// In that case, the C flag instead means that the operation acts as a rekey or ratchet.
	/// </summary>
	C = 4,

	/// <summary>
	/// Transport.
	/// If set, this flag means that the operation sends or receives data using the transport.
	/// An operation has T set if and only if it has send or recv in its name.
	/// An operation with I and T both set receives data from the transport.
	/// An operation with T set but I clear sends data to the transport.
	/// </summary>
	T = 8,

	/// <summary>
	/// Meta.
	/// If set, this flag means that the operation is handling framing, transcript comments or some other sort of protocol metadata.
	/// It doesn't affect how the operation is performed.
	/// </summary>
	M = 16,

	/// <summary>
	/// Keytree.
	/// This flag is reserved for a certain protocol-level countermeasure against side-channel analysis.
	/// It does affect how an operation is performed.
	/// This specification does not describe its use. For all operations in this specification, the K flag must be clear.
	/// </summary>
	K = 32
}
