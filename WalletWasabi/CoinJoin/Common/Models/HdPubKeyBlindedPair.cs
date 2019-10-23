using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.CoinJoin.Common.Models
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
