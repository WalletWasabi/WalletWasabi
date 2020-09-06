using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.BitcoinCore.Rpc.Models
{
	public class VerboseTransactionInfo
	{
		public VerboseTransactionInfo(uint256 id, IEnumerable<VerboseInputInfo> inputs, IEnumerable<VerboseOutputInfo> outputs)
		{
			Id = id;
			Inputs = inputs;
			Outputs = outputs;
		}

		public uint256 Id { get; }

		public IEnumerable<VerboseInputInfo> Inputs { get; }

		public IEnumerable<VerboseOutputInfo> Outputs { get; }
	}
}
