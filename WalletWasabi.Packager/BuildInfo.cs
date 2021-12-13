using System.Text.Json.Serialization;

namespace WalletWasabi.Packager
{
	public class BuildInfo
	{
		[JsonConstructor]
		public BuildInfo(string netRuntimeVersion, string netSdkVersion, string gitCommitHash)
		{
			NetRuntimeVersion = netRuntimeVersion;
			NetSdkVersion = netSdkVersion;
			GitCommitHash = gitCommitHash;
		}

		[JsonPropertyName("NetRuntimeVersion")]
		public string NetRuntimeVersion { get; }

		[JsonPropertyName("NetSdkVersion")]
		public string NetSdkVersion { get; }

		[JsonPropertyName("GitCommitHash")]
		public string GitCommitHash { get; }
	}
}
