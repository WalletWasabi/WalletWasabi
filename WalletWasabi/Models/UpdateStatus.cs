using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models
{
	public class UpdateStatus : IEquatable<UpdateStatus>
	{
		public bool ClientUpToDate { get; private set; }
		public bool BackendCompatible { get; private set; }
		public Version LegalDocumentsVersion { get; private set; }

		public UpdateStatus(bool backendCompatible, bool clientUpToDate, Version legalDocumentsVersion)
		{
			BackendCompatible = backendCompatible;
			ClientUpToDate = clientUpToDate;
			LegalDocumentsVersion = legalDocumentsVersion;
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as UpdateStatus);

		public bool Equals(UpdateStatus other) => this == other;

		public override int GetHashCode() => ClientUpToDate.GetHashCode() ^ BackendCompatible.GetHashCode() ^ LegalDocumentsVersion.GetHashCode();

		public static bool operator ==(UpdateStatus x, UpdateStatus y) => y?.ClientUpToDate == x?.ClientUpToDate && y?.BackendCompatible == x?.BackendCompatible && y?.LegalDocumentsVersion == x?.LegalDocumentsVersion;

		public static bool operator !=(UpdateStatus x, UpdateStatus y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
