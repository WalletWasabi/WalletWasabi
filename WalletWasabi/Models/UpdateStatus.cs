using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models
{
	public class UpdateStatus : IEquatable<UpdateStatus>
	{
		public UpdateStatus(bool backendCompatible, bool clientUpToDate, Version legalDocumentsVersion, ushort currentBackendMajorVersion)
		{
			BackendCompatible = backendCompatible;
			ClientUpToDate = clientUpToDate;
			LegalDocumentsVersion = legalDocumentsVersion;
			CurrentBackendMajorVersion = currentBackendMajorVersion;
		}

		public bool ClientUpToDate { get; }
		public bool BackendCompatible { get; }

		public Version LegalDocumentsVersion { get; }
		public ushort CurrentBackendMajorVersion { get; }

		#region EqualityAndComparison

		public static bool operator ==(UpdateStatus x, UpdateStatus y)
			=> (x?.ClientUpToDate, x?.BackendCompatible, x?.LegalDocumentsVersion, x?.CurrentBackendMajorVersion) == (y?.ClientUpToDate, y?.BackendCompatible, y?.LegalDocumentsVersion, y?.CurrentBackendMajorVersion);

		public static bool operator !=(UpdateStatus x, UpdateStatus y) => !(x == y);

		public override bool Equals(object obj) => Equals(obj as UpdateStatus);

		public bool Equals(UpdateStatus other) => this == other;

		public override int GetHashCode() => (ClientUpToDate, BackendCompatible, LegalDocumentsVersion, CurrentBackendMajorVersion).GetHashCode();

		#endregion EqualityAndComparison
	}
}
