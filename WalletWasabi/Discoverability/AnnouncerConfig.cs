using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.Discoverability
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AnnouncerConfig
	{
		[JsonProperty(PropertyName = "IsEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsEnabled { get; set; }

		[JsonProperty(PropertyName = "CoordinatorDescription")]
		public string CoordinatorDescription { get; set; } = "WabiSabi Coinjoin Coordinator";

		[JsonProperty(PropertyName = "CoordinatorUri")]
		public string CoordinatorUri { get; set; } = "https://api.example.com/";

		[JsonProperty(PropertyName = "RelayUris")]
		public string[] RelayUris { get; set; } = { "wss://relay.primal.net" };

		[JsonProperty(PropertyName = "KeyBytes")]
		public byte[]? KeyBytes { get; set; } = InitKeyBytes();

		private static byte[] InitKeyBytes()
		{
			using var key = new Key();
			return key.ToBytes();
		}
	}
}
