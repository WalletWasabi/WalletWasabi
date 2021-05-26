using System.Collections.Generic;

namespace WalletWasabi.Tor.Control.Messages.CircuitStatus
{
	/// <remarks>
	/// Note that the <see cref="CircuitID"/> and <see cref="CircStatus"/> are then only mandatory
	/// fields in <c>GETINFO circuit-status</c> reply.
	/// </remarks>
	public class CircuitInfo
	{
		public CircuitInfo(string circuitID, CircStatus circStatus)
		{
			CircuitID = circuitID;
			CircStatus = circStatus;
		}

		/// <summary>Unique circuit identifier</summary>
		/// <remarks>
		/// Currently, Tor only uses digits, but this may change.
		/// <para>String matches <c>^[a-zA-Z0-9]{1,16}$</c>.</para>
		/// </remarks>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">2.4. General-use tokens</seealso>
		public string CircuitID { get; }
		public CircStatus CircStatus { get; }
		public List<CircPath> CircPaths { get; init; } = new();
		public BuildFlag? BuildFlag { get; init; }
		public Purpose? Purpose { get; init; }
		public HsState? HsState { get; init; }
		public string? RendQuery { get; init; }
		public string? TimeCreated { get; init; }
		public Reason? Reason { get; init; }
		public Reason? RemoteReason { get; init; }
		public string? UserName { get; init; }
		public string? UserPassword { get; init; }
	}
}
