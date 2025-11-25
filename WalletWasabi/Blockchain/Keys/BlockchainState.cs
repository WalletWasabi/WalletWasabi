using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Keys;

public class BlockchainState
{
	public BlockchainState(Network network, Height height)
	{
		Network = network;
		Height = Height.Max(height, new Height(SmartHeader.GetStartingHeader(Network).Height));
	}

	public BlockchainState()
		: this(Network.Main, 0)
	{
	}

	public BlockchainState(Network network) : this(network, height: 0)
	{
	}

	public Network Network { get; }

	public Height Height { get; set; }
}
