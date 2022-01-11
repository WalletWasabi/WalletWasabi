namespace WalletWasabi.Tor.Control;

/// <summary>
/// An enumerator containing the status codes sent in response to commands.
/// </summary>
/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section "4. Replies".</seealso>
public enum StatusCode : int
{
	/// <summary>The command was processed.</summary>
	OK = 250,

	/// <summary>The operation was unnecessary.</summary>
	OperationUnnecessary = 251,

	/// <summary>The resources were exhausted.</summary>
	ResourceExhausted = 451,

	/// <summary>There was a syntax error in the protocol.</summary>
	SyntaxErrorProtocol = 500,

	/// <summary>The command was unrecognized.</summary>
	UnrecognizedCommand = 510,

	/// <summary>The command is unimplemented.</summary>
	UnimplementedCommand = 511,

	/// <summary>There was a syntax error in a command argument.</summary>
	SyntaxErrorArgument = 512,

	/// <summary>The command argument was unrecognized.</summary>
	UnrecognizedCommandArgument = 513,

	/// <summary>The command could not execute because authentication is required.</summary>
	AuthenticationRequired = 514,

	/// <summary>The command to authenticate returned an invalid authentication response.</summary>
	BadAuthentication = 515,

	/// <summary>The command generated a non-specific error response.</summary>
	Unspecified = 550,

	/// <summary>An error occurred within Tor leading to the command failing to execute.</summary>
	InternalError = 551,

	/// <summary>The command contained a configuration key, stream ID, circuit ID, or event which did not exist.</summary>
	UnrecognizedEntity = 552,

	/// <summary>The command sent a configuration value incompatible with the configuration.</summary>
	InvalidConfigurationValue = 553,

	/// <summary>The command contained an invalid descriptor.</summary>
	InvalidDescriptor = 554,

	/// <summary>The command contained a reference to an unmanaged entity.</summary>
	UnmanagedEntity = 555,

	/// <summary>A notification sent following an asynchronous operation.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">4.1. Asynchronous events</seealso>
	AsynchronousEventNotify = 650,
}
