using NBitcoin;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Blockchain.Keys;

public class BlockchainState
{
	public BlockchainState(Network? network = null, ChainHeight? height = null)
	{
		Network = network ?? Network.Main;
		Height = ChainHeight.Max(height ?? ChainHeight.Genesis, new ChainHeight(SmartHeader.GetStartingHeader(Network).Height));
	}

	public Network Network { get; }

	public ChainHeight Height { get; set; }
}
