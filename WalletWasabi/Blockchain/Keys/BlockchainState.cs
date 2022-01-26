using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class BlockchainState
{
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

	public BlockchainState(Network network) : this(network, 0)
	{
	}

	[JsonProperty]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; set; }

	[JsonProperty]
	[JsonConverter(typeof(HeightJsonConverter))]
	public Height Height { get; set; }
}
