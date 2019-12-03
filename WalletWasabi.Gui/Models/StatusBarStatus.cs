using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models
{
	public class StatusBarStatus : IEquatable<StatusBarStatus>
	{
		public StatusBarStatusType Type { get; }
		public int ProgressPercentage { get; }

		public StatusBarStatus(StatusBarStatusType type, int progressPercentage = -1)
		{
			Type = type;
			ProgressPercentage = progressPercentage;
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is StatusBarStatus rpcStatus && this == rpcStatus;

		public bool Equals(StatusBarStatus other) => this == other;

		public override int GetHashCode() => Type.GetHashCode() ^ ProgressPercentage.GetHashCode();

		public static bool operator ==(StatusBarStatus x, StatusBarStatus y) => y?.Type == x?.Type && y?.ProgressPercentage == x?.ProgressPercentage;

		public static bool operator !=(StatusBarStatus x, StatusBarStatus y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
