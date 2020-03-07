using System.Collections.Generic;

namespace NBitcoin.RPC
{
	public class VerboseBlockInfo
	{
		public uint256 Hash { get; set; }
		public uint256 PrevBlockHash { get; set; }
		public ulong Confirmations { get; set; }
		public ulong Height { get; set; }
		public List<VerboseTransactionInfo> Transactions { get; set; } = new List<VerboseTransactionInfo>();
	}

	public class VerboseTransactionInfo
	{
		public uint256 Id { get; set; }

		public List<VerboseInputInfo> Inputs { get; set; } = new List<VerboseInputInfo>();
		public List<VerboseOutputInfo> Outputs { get; set; } = new List<VerboseOutputInfo>();

	}

	public class VerboseInputInfo
	{
		public OutPoint OutPoint { get; set; }
		public VerboseOutputInfo PrevOutput { get; set; }
	}

	public class VerboseOutputInfo
	{
		public Money Value { get; set; }
		public Script ScriptPubKey { get; set; }
	}
}