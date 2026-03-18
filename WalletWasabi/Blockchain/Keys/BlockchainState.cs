using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;

namespace WalletWasabi.Blockchain.Keys;

public class BlockchainState
{
	public BlockchainState(Network? network = null, ChainHeight? height = null, ChainHeight? birthHeight = null)
	{
		Network = network ?? Network.Main;
		Height = ChainHeight.Max(height ?? ChainHeight.Genesis, FilterCheckpoints.GetWasabiGenesisFilter(Network).Header.Height);
		BirthHeight = birthHeight;
	}

	public Network Network { get; }

	public ChainHeight Height { get; set; }

	public ChainHeight? BirthHeight { get; }
}
