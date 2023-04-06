using System.Runtime.Serialization;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class BlockchainState
{
	[JsonConstructor]
	public BlockchainState(Network network, Height height, Height nonObsoleteHeight)
	{
		Network = network;
		Height = height;
		NonObsoleteHeight = nonObsoleteHeight;
	}

	public BlockchainState()
	{
		Network = Network.Main;
		Height = 0;
		NonObsoleteHeight = Height.Unknown;
	}

	public BlockchainState(Network network) : this(network, 0, Height.Unknown)
	{
	}

	[JsonProperty]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; set; }

	[JsonProperty]
	[JsonConverter(typeof(WalletHeightJsonConverter))]
	public Height Height { get; set; }
	
	[JsonProperty]
	[JsonConverter(typeof(WalletHeightJsonConverter))]
	public Height NonObsoleteHeight { get; set; }
}
