using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.KeyManagement
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockchainState
	{
		[JsonProperty(Order = 0)]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height LastBlockHeight { get; }

		[JsonProperty(Order = 1)]
		public List<BlockState> BlockStates { get; }

		[JsonConstructor]
		public BlockchainState(Height lastBlockHeight, IEnumerable<BlockState> blockStates)
		{
			LastBlockHeight = lastBlockHeight;
			BlockStates = blockStates?.OrderBy(x => x).ToList() ?? new List<BlockState>();
		}

		public BlockchainState()
		{
			LastBlockHeight = 0;
			BlockStates = new List<BlockState>();
		}
	}
}
