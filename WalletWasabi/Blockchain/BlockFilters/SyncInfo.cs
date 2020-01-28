using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.BlockFilters
{
	public class SyncInfo
	{
		public BlockchainInfo BlockchainInfo { get; }
		public int BlockCount { get; }
		public DateTimeOffset BlockchainInfoUpdated { get; }
		public bool IsCoreSynchornized { get; }

		public SyncInfo(BlockchainInfo bcinfo)
		{
			Guard.NotNull(nameof(bcinfo), bcinfo);
			BlockCount = (int)bcinfo.Blocks;
			int headerCount = (int)bcinfo.Headers;
			BlockchainInfoUpdated = DateTimeOffset.UtcNow;
			IsCoreSynchornized = BlockCount == headerCount;
		}
	}
}
