using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models;

public class RoundStateResponse4 : RoundStateResponseBase
{
	[JsonProperty(ItemConverterType = typeof(PubKeyJsonConverter))]
	public IEnumerable<PubKey> SignerPubKeys { get; set; }

	public IEnumerable<PublicNonceWithIndex> RPubKeys { get; set; }

	public override int MixLevelCount => SignerPubKeys.Count();
}
