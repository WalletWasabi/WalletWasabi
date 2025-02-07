using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class BlockchainState
{
	[JsonConstructor]
	public BlockchainState(Network network, Height height, Height turboSyncHeight)
	{
		Network = network;
		Height = height;
		TurboSyncHeight = turboSyncHeight;
	}

	public BlockchainState()
	{
		Network = Network.Main;
		Height = 0;
		TurboSyncHeight = 0;
	}

	public BlockchainState(Network network) : this(network, height: 0, turboSyncHeight: 0)
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
	public Height TurboSyncHeight { get; set; }
}
