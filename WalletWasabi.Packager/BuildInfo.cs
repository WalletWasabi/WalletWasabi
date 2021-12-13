using System.Text.Json.Serialization;

namespace WalletWasabi.Packager
{
	public class BuildInfo
	{
		[JsonConstructor]
		public BuildInfo(string netRuntime, string netSdkVersion, string gitCommitHash)
		{
			NetRuntime = netRuntime;
			NetSdkVersion = netSdkVersion;
			GitCommitHash = gitCommitHash;
		}

		[JsonPropertyName("NetRuntimeVersion")]
		public string NetRuntime { get; }

		[JsonPropertyName("NetSdkVersion")]
		public string NetSdkVersion { get; }

		[JsonPropertyName("GitCommitHash")]
		public string GitCommitHash { get; }
	}
}
