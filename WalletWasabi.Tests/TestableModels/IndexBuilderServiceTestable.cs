using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Tests.TestableModels
{
	internal class IndexBuilderServiceTestable : IndexBuilderService
	{
		public IndexBuilderServiceTestable(IRPCClient rpc, BlockNotifier blockNotifier, string indexFilePath) : base(rpc, blockNotifier, indexFilePath)
		{
		}

		public void SetLastFilterBuildTime(DateTimeOffset timeOffset)
		{
			LastFilterBuildTime = timeOffset;
		}
	}
}
