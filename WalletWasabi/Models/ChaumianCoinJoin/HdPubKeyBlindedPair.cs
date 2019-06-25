using Newtonsoft.Json;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
    [JsonObject(MemberSerialization.OptIn)]
	public class HdPubKeyBlindedPair
	{
		[JsonProperty]
		public HdPubKey Key { get; set; }

		[JsonProperty]
		public bool IsBlinded { get; set; }

		[JsonConstructor]
		public HdPubKeyBlindedPair(HdPubKey key, bool isBlinded)
		{
			Key = Guard.NotNull(nameof(key), key);
			IsBlinded = isBlinded;
		}
	}
}
