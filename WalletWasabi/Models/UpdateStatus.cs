using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models
{
	public class UpdateStatus : IEquatable<UpdateStatus>
	{
		public bool ClientUpToDate { get; private set; }
		public bool BackendCompatible { get; private set; }

		public UpdateStatus(bool backendCompatible, bool clientUpToDate)
		{
			BackendCompatible = backendCompatible;
			ClientUpToDate = clientUpToDate;
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is UpdateStatus updateStatus && this == updateStatus;

		public bool Equals(UpdateStatus other) => this == other;

		public override int GetHashCode() => ClientUpToDate.GetHashCode() ^ BackendCompatible.GetHashCode();

		public static bool operator ==(UpdateStatus x, UpdateStatus y) => y?.ClientUpToDate == x?.ClientUpToDate && y?.BackendCompatible == x?.BackendCompatible;

		public static bool operator !=(UpdateStatus x, UpdateStatus y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
