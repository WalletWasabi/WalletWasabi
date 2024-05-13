namespace WalletWasabi.Models;

public class UpdateStatus : IEquatable<UpdateStatus>
{
	public UpdateStatus(bool backendCompatible, ushort currentBackendMajorVersion)
	{
		BackendCompatible = backendCompatible;
		CurrentBackendMajorVersion = currentBackendMajorVersion;
	}

	public bool ClientUpToDate { get; set; }
	public bool BackendCompatible { get; }
	public bool IsReadyToInstall { get; set; }

	public ushort CurrentBackendMajorVersion { get; }

	public Version ClientVersion { get; set; } = new(0, 0, 0);

	#region EqualityAndComparison

	public static bool operator ==(UpdateStatus? x, UpdateStatus? y)
		=> (x?.ClientUpToDate, x?.BackendCompatible, x?.CurrentBackendMajorVersion, x?.ClientVersion) == (y?.ClientUpToDate, y?.BackendCompatible, y?.CurrentBackendMajorVersion, y?.ClientVersion);

	public static bool operator !=(UpdateStatus? x, UpdateStatus? y) => !(x == y);

	public override bool Equals(object? obj) => Equals(obj as UpdateStatus);

	public bool Equals(UpdateStatus? other) => this == other;

	public override int GetHashCode() => (ClientUpToDate, BackendCompatible, CurrentBackendMajorVersion, ClientVersion).GetHashCode();

	#endregion EqualityAndComparison
}
