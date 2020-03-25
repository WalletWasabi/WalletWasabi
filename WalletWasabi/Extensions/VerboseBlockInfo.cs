using System;
using System.Collections.Generic;

namespace NBitcoin.RPC
{
	public class VerboseBlockInfo
	{
		public DateTimeOffset BlockTime { get; set; }
		public uint256 Hash { get; set; }
		public uint256 PrevBlockHash { get; set; }
		public ulong Confirmations { get; set; }
		public ulong Height { get; set; }
		public List<VerboseTransactionInfo> Transactions { get; set; } = new List<VerboseTransactionInfo>();
	}
}
