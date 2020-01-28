using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlockchainState
	{
		[JsonProperty]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; set; }

		[JsonProperty]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height { get; set; }

		[JsonConstructor]
		public BlockchainState(Network network, Height height)
		{
			Network = network;
			Height = height;
		}

		public BlockchainState()
		{
			Network = Network.Main;
			Height = 0;
		}
	}
}
