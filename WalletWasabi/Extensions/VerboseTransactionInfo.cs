using System.Collections.Generic;

namespace NBitcoin.RPC
{
	public class VerboseTransactionInfo
	{
		public uint256 Id { get; set; }

		public List<VerboseInputInfo> Inputs { get; set; } = new List<VerboseInputInfo>();
		public List<VerboseOutputInfo> Outputs { get; set; } = new List<VerboseOutputInfo>();
	}
}