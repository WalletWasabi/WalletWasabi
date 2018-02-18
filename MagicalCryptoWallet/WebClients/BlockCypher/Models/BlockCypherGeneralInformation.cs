using MagicalCryptoWallet.Converters;
using MagicalCryptoWallet.Models;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.WebClients.BlockCypher.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockCypherGeneralInformation
    {
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }

		[JsonProperty(PropertyName = "height")]
		[JsonConverter(typeof(HeightConverter))]
		public Height Height { get; set; }

		[JsonProperty(PropertyName = "hash")]
		[JsonConverter(typeof(Uint256Converter))]
		public uint256 Hash { get; set; }

		[JsonProperty(PropertyName = "time")]
		[JsonConverter(typeof(BlockCypherDateTimeOffsetConverter))]
		public DateTimeOffset Time { get; set; }

		[JsonProperty(PropertyName = "latest_url")]
		[JsonConverter(typeof(BlockCypherUriConverter))]
		public Uri LatestUrl { get; set; }

		[JsonProperty(PropertyName = "previous_hash")]
		[JsonConverter(typeof(Uint256Converter))]
		public uint256 PreviousHash { get; set; }

		[JsonProperty(PropertyName = "previous_url")]
		[JsonConverter(typeof(BlockCypherUriConverter))]
		public Uri PreviousUrl { get; set; }

		[JsonProperty(PropertyName = "peer_count")]
		public int PeerCount { get; set; }

		[JsonProperty(PropertyName = "unconfirmed_count")]
		public long UnconfirmedCount { get; set; }

		[JsonProperty(PropertyName = "high_fee_per_kb")]
		[JsonConverter(typeof(FeeRatePerKbConverter))]
		public FeeRate HighFee { get; set; }

		[JsonProperty(PropertyName = "medium_fee_per_kb")]
		[JsonConverter(typeof(FeeRatePerKbConverter))]
		public FeeRate MediumFee { get; set; }

		[JsonProperty(PropertyName = "low_fee_per_kb")]
		[JsonConverter(typeof(FeeRatePerKbConverter))]
		public FeeRate LowFee { get; set; }

		[JsonProperty(PropertyName = "last_fork_height")]
		[JsonConverter(typeof(HeightConverter))]
		public Height LastForkHeight { get; set; }

		[JsonProperty(PropertyName = "last_fork_hash")]
		[JsonConverter(typeof(Uint256Converter))]
		public uint256 LastForkHash { get; set; }
    }
}
