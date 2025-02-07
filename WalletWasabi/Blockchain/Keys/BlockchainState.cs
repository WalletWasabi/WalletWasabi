using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

public class BlockchainState
{
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

	public Network Network { get; set; }

	public Height Height { get; set; }

	public Height TurboSyncHeight { get; set; }
}
