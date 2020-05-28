using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models
{
	public class UpdateStatus : IEquatable<UpdateStatus>
	{
		public UpdateStatus(bool backendCompatible, bool clientUpToDate, Version legalDocumentsVersion, string currentBackendApiVersion)
		{
			BackendCompatible = backendCompatible;
			ClientUpToDate = clientUpToDate;
			LegalDocumentsVersion = legalDocumentsVersion;
			CurrentBackendApiVersion = currentBackendApiVersion;
		}

		public bool ClientUpToDate { get; private set; }
		public bool BackendCompatible { get; private set; }

		public Version LegalDocumentsVersion { get; private set; }
		public string CurrentBackendApiVersion { get; private set; }

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as UpdateStatus);

		public bool Equals(UpdateStatus other) => this == other;

		public override int GetHashCode() => (ClientUpToDate, BackendCompatible, LegalDocumentsVersion, CurrentBackendApiVersion).GetHashCode();

		public static bool operator ==(UpdateStatus x, UpdateStatus y)
			=> (x?.ClientUpToDate, x?.BackendCompatible, x?.LegalDocumentsVersion, x?.CurrentBackendApiVersion) == (y?.ClientUpToDate, y?.BackendCompatible, y?.LegalDocumentsVersion, y?.CurrentBackendApiVersion);

		public static bool operator !=(UpdateStatus x, UpdateStatus y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
