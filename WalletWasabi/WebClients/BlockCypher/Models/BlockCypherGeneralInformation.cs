using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.WebClients.BlockCypher.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockCypherGeneralInformation
	{
		[JsonProperty(PropertyName = nameof(Name))]
		public string Name { get; set; }

		[JsonProperty(PropertyName = nameof(Height))]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height { get; set; }

		[JsonProperty(PropertyName = nameof(Hash))]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 Hash { get; set; }

		[JsonProperty(PropertyName = nameof(Time))]
		[JsonConverter(typeof(BlockCypherDateTimeOffsetJsonConverter))]
		public DateTimeOffset Time { get; set; }

		[JsonProperty(PropertyName = "latest_url")]
		[JsonConverter(typeof(BlockCypherUriJsonConverter))]
		public Uri LatestUrl { get; set; }

		[JsonProperty(PropertyName = "previous_hash")]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 PreviousHash { get; set; }

		[JsonProperty(PropertyName = "previous_url")]
		[JsonConverter(typeof(BlockCypherUriJsonConverter))]
		public Uri PreviousUrl { get; set; }

		[JsonProperty(PropertyName = "peer_count")]
		public int PeerCount { get; set; }

		[JsonProperty(PropertyName = "unconfirmed_count")]
		public long UnconfirmedCount { get; set; }

		[JsonProperty(PropertyName = "high_fee_per_kb")]
		[JsonConverter(typeof(FeeRatePerKbJsonConverter))]
		public FeeRate HighFee { get; set; }

		[JsonProperty(PropertyName = "medium_fee_per_kb")]
		[JsonConverter(typeof(FeeRatePerKbJsonConverter))]
		public FeeRate MediumFee { get; set; }

		[JsonProperty(PropertyName = "low_fee_per_kb")]
		[JsonConverter(typeof(FeeRatePerKbJsonConverter))]
		public FeeRate LowFee { get; set; }

		[JsonProperty(PropertyName = "last_fork_height")]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height LastForkHeight { get; set; }

		[JsonProperty(PropertyName = "last_fork_hash")]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 LastForkHash { get; set; }
	}
}
