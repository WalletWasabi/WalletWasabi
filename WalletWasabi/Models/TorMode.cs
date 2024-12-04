namespace WalletWasabi.Models;

/// <summary>
/// Modes for how we interact with Tor.
/// </summary>
public enum TorMode
{
	/// <summary>Tor is disabled. Clearnet is used instead.</summary>
	[FriendlyName("Disabled")]
	Disabled,

	/// <summary>Use running Tor or start a new Tor process if it is not running.</summary>
	/// <remarks>In this mode, Wasabi app is the owner of its Tor process.</remarks>
	[FriendlyName("Enabled")]
	Enabled,

	/// <summary>Use only running Tor process.</summary>
	/// <remarks>
	/// In this mode, Wasabi app is not the owner of its Tor process.
	/// <para>Useful for distributions like Whonix or Tails where starting a new Tor process is not a good option.</para>
	/// </remarks>
	[FriendlyName("Connect Only")]
	EnabledOnlyRunning,
}
