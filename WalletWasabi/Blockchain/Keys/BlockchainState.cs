using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockchainState
	{
		[JsonProperty(Order = 0)]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; set; }

		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height BestHeight { get; set; }

		[JsonProperty(Order = 2)]
		public List<BlockState> BlockStates { get; }

		[JsonConstructor]
		public BlockchainState(Network network, Height bestHeight, IEnumerable<BlockState> blockStates)
		{
			Network = network;
			BestHeight = bestHeight;
			BlockStates = blockStates?.OrderBy(x => x).ToList() ?? new List<BlockState>();
		}

		public BlockchainState()
		{
			Network = Network.Main;
			BestHeight = 0;
			BlockStates = new List<BlockState>();
		}
	}
}
