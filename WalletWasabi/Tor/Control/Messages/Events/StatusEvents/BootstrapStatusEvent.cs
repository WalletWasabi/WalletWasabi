using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace WalletWasabi.Tor.Control.Messages.Events.StatusEvents;

/// <summary>Status event of type <see cref="StatusType.STATUS_CLIENT"/> and ACTION=BOOTSTRAP.</summary>
public record BootstrapStatusEvent : StatusEvent
{
	/// <remarks>See https://gitweb.torproject.org/torspec.git/tree/control-spec.txt 5.5.2</remarks>
	public static readonly ImmutableDictionary<int, string> Phases = ImmutableDictionary<int, string>.Empty
		.Add(0, "tag=starting: Starting")
		.Add(1, "tag=conn_pt: Connecting to pluggable transport")
		.Add(2, "tag=conn_done_pt: Connected to pluggable transport")
		.Add(3, "tag=conn_proxy: Connecting to proxy")
		.Add(4, "tag=conn_done_proxy: Connected to proxy")
		.Add(5, "tag=conn: Connecting to a relay")
		.Add(10, "tag=conn_done: Connected to a relay")
		.Add(14, "tag=handshake: Handshaking with a relay")
		.Add(15, "tag=handshake_done: Handshake with a relay done")
		.Add(20, "tag=onehop_create: Establishing an encrypted directory connection")
		.Add(25, "tag=requesting_status: Asking for networkstatus consensus")
		.Add(30, "tag=loading_status: Loading networkstatus consensus")
		.Add(40, "tag=loading_keys: Loading authority key certs")
		.Add(45, "tag=requesting_descriptors: Asking for relay descriptors")
		.Add(50, "tag=loading_descriptors: Loading relay descriptors")
		.Add(75, "tag=enough_dirinfo: Loaded enough directory info to build")
		.Add(76, "tag=ap_conn_pt: Connecting to pluggable transport to build")
		.Add(77, "tag=ap_conn_done_pt: Connected to pluggable transport to build circuits")
		.Add(78, "tag=ap_conn_proxy: Connecting to proxy to build circuits")
		.Add(79, "tag=ap_conn_done_proxy: Connected to proxy to build circuits")
		.Add(80, "tag=ap_conn: Connecting to a relay to build circuits")
		.Add(85, "tag=ap_conn_done: Connected to a relay to build circuits")
		.Add(89, "tag=ap_handshake: Finishing handshake with a relay to build circuits")
		.Add(90, "tag=ap_handshake_done: Handshake finished with a relay to build circuits")
		.Add(95, "tag=circuit_create: Establishing a[n internal] Tor circuit")
		.Add(100, "tag=done: Done");

	public BootstrapStatusEvent(string action, Dictionary<string, string> arguments) : base(action, arguments)
	{
	}

	/// <summary>A number between <c>0</c> and <c>100</c>.</summary>
	public int Progress => int.Parse(Arguments["PROGRESS"], NumberStyles.None, NumberFormatInfo.InvariantInfo);

	/// <summary>Denotes a bootstrap phase.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">5.5.2. Phases in Bootstrap Stage 1.</seealso>
	/// <example><c>starting</c> for phase 0, <c>handshake</c> for phase 14, <c>done</c> for phase 100</example>
	public string Tag => Arguments["TAG"];

	/// <summary>String that can be displayed to the user to describe the *next* task that Tor will tackle.</summary>
	/// <remarks>That is, the task it is working on after sending the status event.</remarks>
	public string Summary => Arguments["SUMMARY"];

	/// <summary>Human readable string with any hints Tor has to offer about why it's having troubles bootstrapping or <c>null</c>.</summary>
	public string? Warning
	{
		get
		{
			_ = Arguments.TryGetValue("WARNING", out string? result);
			return result;
		}
	}

	/// <summary>Long-term-stable controller-facing tags to identify particular issues in a bootstrapping step or <c>null</c>.</summary>
	public string? Reason
	{
		get
		{
			_ = Arguments.TryGetValue("REASON", out string? result);
			return result;
		}
	}

	/// <remarks>
	/// Two values are possible at the moment:
	/// <list type="bullet">
	/// <item><c>ignore</c> - the controller can accumulate the string in a pile of problems to show the user if the user asks,</item>
	/// <item><c>warn</c> - the controller should alert the user that Tor is pretty sure there's a bootstrapping problem.</item>
	/// </list>
	/// </remarks>
	public string? Recommendation
	{
		get
		{
			_ = Arguments.TryGetValue("RECOMMENDATION", out string? result);
			return result;
		}
	}
}
