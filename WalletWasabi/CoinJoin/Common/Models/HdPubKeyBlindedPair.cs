using Newtonsoft.Json;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Common.Models;

[JsonObject(MemberSerialization.OptIn)]
public class HdPubKeyBlindedPair
{
	[JsonConstructor]
	public HdPubKeyBlindedPair(HdPubKey key, bool isBlinded)
	{
		Key = Guard.NotNull(nameof(key), key);
		IsBlinded = isBlinded;
	}

	[JsonProperty]
	public HdPubKey Key { get; set; }

	[JsonProperty]
	public bool IsBlinded { get; set; }
}
