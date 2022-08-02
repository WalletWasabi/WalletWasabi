namespace WalletWasabi.Models;

public class UpdateStatus : IEquatable<UpdateStatus>
{
	public UpdateStatus(bool backendCompatible, bool clientUpToDate, Version legalDocumentsVersion, ushort currentBackendMajorVersion, Version clientVersion)
	{
		BackendCompatible = backendCompatible;
		ClientUpToDate = clientUpToDate;
		LegalDocumentsVersion = legalDocumentsVersion;
		CurrentBackendMajorVersion = currentBackendMajorVersion;
		ClientVersion = clientVersion;
	}

	public bool ClientUpToDate { get; }
	public bool BackendCompatible { get; }
	public bool IsReadyToInstall { get; set; }

	public Version LegalDocumentsVersion { get; }
	public ushort CurrentBackendMajorVersion { get; }

	public Version ClientVersion { get; set; }

	#region EqualityAndComparison

	public static bool operator ==(UpdateStatus? x, UpdateStatus? y)
		=> (x?.ClientUpToDate, x?.BackendCompatible, x?.LegalDocumentsVersion, x?.CurrentBackendMajorVersion, x?.ClientVersion) == (y?.ClientUpToDate, y?.BackendCompatible, y?.LegalDocumentsVersion, y?.CurrentBackendMajorVersion, y?.ClientVersion);

	public static bool operator !=(UpdateStatus? x, UpdateStatus? y) => !(x == y);

	public override bool Equals(object? obj) => Equals(obj as UpdateStatus);

	public bool Equals(UpdateStatus? other) => this == other;

	public override int GetHashCode() => (ClientUpToDate, BackendCompatible, LegalDocumentsVersion, CurrentBackendMajorVersion, ClientVersion).GetHashCode();

	#endregion EqualityAndComparison
}
