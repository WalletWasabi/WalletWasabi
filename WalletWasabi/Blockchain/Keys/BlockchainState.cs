using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockchainState
	{
		[JsonConstructor]
		public BlockchainState(Network network, Height height)
		{
			Network = network;
			Height = height;
		}

		public BlockchainState(Network network)
		{
			Network = network;
			Height = 0;
		}

		[JsonProperty]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; private set; }

		[JsonProperty]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height { get; set; }
	}
}
