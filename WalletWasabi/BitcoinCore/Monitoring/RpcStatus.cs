using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class RpcStatus : IEquatable<RpcStatus>
	{
		public static RpcStatus Unresponsive = new RpcStatus(false, 0, 0, 0);

		private RpcStatus(bool success, ulong headers, ulong blocks, int peersCount)
		{
			Synchronized = false;
			if (success)
			{
				var diff = headers - blocks;
				if (peersCount == 0)
				{
					Status = "Full node is connecting...";
				}
				else if (diff == 0)
				{
					Synchronized = true;
					Status = "Full node is synchronized";
				}
				else
				{
					Status = $"Full node is downloading {diff} blocks...";
				}
			}
			else
			{
				Status = "Full node is unresponsive";
			}

			Success = success;
			Headers = headers;
			Blocks = blocks;
			PeersCount = peersCount;
		}

		public string Status { get; }
		public bool Success { get; }
		public ulong Headers { get; }
		public ulong Blocks { get; }
		public int PeersCount { get; }
		public bool Synchronized { get; }

		public static RpcStatus Responsive(ulong headers, ulong blocks, int peersCount) => new RpcStatus(true, headers, blocks, peersCount);

		public override string ToString() => Status;

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as RpcStatus);

		public bool Equals(RpcStatus other) => this == other;

		public override int GetHashCode() => Status.GetHashCode();

		public static bool operator ==(RpcStatus x, RpcStatus y) => y?.Status == x?.Status;

		public static bool operator !=(RpcStatus x, RpcStatus y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
