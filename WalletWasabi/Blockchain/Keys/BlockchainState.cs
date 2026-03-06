using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;

namespace WalletWasabi.Blockchain.Keys;

public class BlockchainState
{
	public BlockchainState(Network? network = null, ChainHeight? height = null, ChainHeight? birthdayHeight = null)
	{
		Network = network ?? Network.Main;
		Height = ChainHeight.Max(height ?? ChainHeight.Genesis, FilterCheckpoints.GetWasabiGenesisFilter(Network).Header.Height);
		BirthdayHeight = birthdayHeight;
	}

	public Network Network { get; }

	public ChainHeight Height { get; set; }

	public ChainHeight? BirthdayHeight { get; }
}
