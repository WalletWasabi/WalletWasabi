using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters
{
	public interface IIndexBuilderService
	{
		BlockNotifier BlockNotifier { get; }
		string IndexFilePath { get; }
		bool IsRunning { get; }
		bool IsStopping { get; }
		DateTimeOffset LastFilterBuildTime { get; }
		IRPCClient RpcClient { get; }
		uint StartingHeight { get; }

		(Height bestHeight, IEnumerable<FilterModel> filters) GetFilterLinesExcluding(uint256 bestKnownBlockHash, int count, out bool found);
		FilterModel GetLastFilter();
		Task StopAsync();
		void Synchronize();
	}
}