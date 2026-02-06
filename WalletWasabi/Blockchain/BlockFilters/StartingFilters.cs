using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Blockchain.BlockFilters;

public static class StartingFilters
{
	public static FilterModel GetStartingFilter(Network network)
	{
		var startingHeader = SmartHeader.GetStartingHeader(network);
		if (network == Network.Main)
		{
			return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:02832810ec08a0:{startingHeader.HeaderOrPrevBlockHash}:{startingHeader.EpochBlockTime}");
		}
		else if (network == Network.TestNet)
		{
			// First SegWit block with P2WPKH on TestNet: 00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a
			return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:{startingHeader.HeaderOrPrevBlockHash}:{startingHeader.EpochBlockTime}");
		}
		else if (network == Network.RegTest)
		{
			GolombRiceFilter filter = LegacyWasabiFilterGenerator.CreateDummyEmptyFilter(startingHeader.BlockHash);
			return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:{filter}:{startingHeader.HeaderOrPrevBlockHash}:{startingHeader.EpochBlockTime}");
		}
		else if (network == Bitcoin.Instance.Signet)
		{
			GolombRiceFilter filter = LegacyWasabiFilterGenerator.CreateDummyEmptyFilter(startingHeader.BlockHash);

			// signet genesis block
			return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6:{startingHeader.HeaderOrPrevBlockHash}:{startingHeader.EpochBlockTime}");
		}
		else
		{
			throw new NotSupportedNetworkException(network);
		}
	}
}
