namespace WalletWasabi.Hwi.Models;

/// <summary>
/// List of commands that HWI project supports for various <see href="https://en.wikipedia.org/wiki/Human_interface_device">HID devices</see>.
/// </summary>
/// <remarks>HWI may partially support a hardware device.</remarks>
/// <seealso href="https://github.com/bitcoin-core/HWI/blob/master/hwilib/hwwclient.py"/>
/// <seealso href="https://github.com/bitcoin-core/HWI/blob/master/docs/coldcard.md"/>
/// <seealso href="https://github.com/bitcoin-core/HWI/blob/master/docs/ledger.md"/>
public enum HwiCommands
{
	/// <summary>Get a list of all available hardware wallets.</summary>
	Enumerate,

	/// <summary>
	/// Return the master BIP44 public key.
	///
	/// <para>Retrieve the public key at the "m/44h/0h/0h" derivation path.</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"xpub": &lt;xpub string&gt;"}</code>.
	/// </remarks>
	GetMasterXpub,

	/// <summary>
	/// Sign a partially signed bitcoin transaction (PSBT).
	/// </summary>
	/// <remarks>
	/// Return <code>{"psbt": &lt;base64 psbt string&gt;}</code>.
	/// </remarks>
	SignTx,

	/// <summary>
	/// Return the public key at the BIP32 derivation path.
	///
	/// <remarks>
	/// Return <code>{"xpub": &lt;xpub string&gt;}</code>.
	/// </remarks>
	/// </summary>
	GetXpub,

	/// <summary>
	/// Sign a message (bitcoin message signing).
	///
	/// <para>Sign the message according to the bitcoin message signing standard.</para>
	/// <para>Retrieve the signing key at the specified BIP32 derivation path.</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"signature": &lt;base64 signature string&gt;}</code>.
	/// </remarks>
	SignMessage,

	/// <summary>
	/// Get JSON array of keys that can be imported to Bitcoin Core with <c>importmulti</c>.
	/// </summary>
	/// <remarks>
	/// Notable arguments:
	/// <list type="table">
	/// <item><c>--path</c> - Derivation path, default follows BIP43 convention, e.g. m/84h/0h/0h/1/* with --addr-type wit --internal. If this argument and --internal is not given, both internal and external keypools will be returned.</item>
	/// <item><c>start</c>  - The index to start at.</item>
	/// <item><c>end</c>    - The index to end at.</item>
	/// </list>
	/// </remarks>
	GetKeypool,

	/// <summary>
	/// Display and return the address of specified type.
	///
	/// <para>redeem_script is a hex-string.</para>
	/// <para>Retrieve the public key at the specified BIP32 derivation path.</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"address": &lt;base58 or bech32 address string&gt;}</code>.
	/// </remarks>
	DisplayAddress,

	/// <summary>
	/// Setup the HID device.
	///
	/// <para>Must return a dictionary with the "success" key, possibly including also "error" and "code".</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"success": bool, "error": str, "code": int}</code>.
	/// </remarks>
	Setup,

	/// <summary>
	/// Wipe the HID device.
	///
	/// <para>Must return a dictionary with the "success" key, possibly including also "error" and "code".</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"success": bool, "error": srt, "code": int}</code>.
	/// </remarks>
	Wipe,

	/// <summary>
	/// Restore the HID device from mnemonic.
	///
	/// <para>Must return a dictionary with the "success" key, possibly including also "error" and "code".</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"success": bool, "error": srt, "code": int}</code>.
	/// </remarks>
	Restore,

	/// <summary>
	/// Backup the HID device.
	///
	/// <para>Must return a dictionary with the "success" key, possibly including also "error" and "code".</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"success": bool, "error": srt, "code": int}</code>.
	/// </remarks>
	Backup,

	/// <summary>
	/// Prompt for PIN.
	///
	/// <para>Must return a dictionary with the "success" key, possibly including also "error" and "code".</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"success": bool, "error": srt, "code": int}</code>.
	/// </remarks>
	PromptPin,

	/// <summary>
	/// Send PIN.
	///
	/// <para>Must return a dictionary with the "success" key, possibly including also "error" and "code".</para>
	/// </summary>
	/// <remarks>
	/// Return <code>{"success": bool, "error": srt, "code": int}</code>.
	/// </remarks>
	SendPin
}
