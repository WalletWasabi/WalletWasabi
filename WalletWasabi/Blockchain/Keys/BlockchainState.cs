using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

public class BlockchainState
{
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

	public BlockchainState(Network network) : this(network, height: 0)
	{
	}

	public Network Network { get; set; }

	public Height Height { get; set; }
}
