using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BitcoinAddressBlindedPair
	{
		[JsonProperty]
		[JsonConverter(typeof(BitcoinAddressJsonConverter))]
		public BitcoinAddress Address { get; set; }

		[JsonProperty]
		public bool IsBlinded { get; set; }

		[JsonConstructor]
		public BitcoinAddressBlindedPair(BitcoinAddress address, bool isBlinded)
		{
			Address = Guard.NotNull(nameof(address), address);
			IsBlinded = isBlinded;
		}
	}
}
