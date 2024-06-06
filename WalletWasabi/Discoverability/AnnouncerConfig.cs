using NBitcoin;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using NNostr.Client.Protocols;

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

		[JsonProperty(PropertyName = "Key")]
		public string Key { get; set; } = InitKey();

		private static string InitKey()
		{
			using var key = new Key();
			using var privKey = ECPrivKey.Create(key.ToBytes());
			return privKey.ToNIP19();
		}
	}
}
