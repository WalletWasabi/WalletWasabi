using System.Collections.Generic;
using System.Globalization;

namespace WalletWasabi.Tor.Control.Messages.Events.StatusEvents
{
	/// <summary>Status event of type <see cref="StatusType.STATUS_CLIENT"/> and ACTION=BOOTSTRAP.</summary>
	public record BootstrapStatusEvent : StatusEvent
	{
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
}
