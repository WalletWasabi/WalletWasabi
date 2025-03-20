using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.BitcoinRpc.Models;

public class VerboseTransactionInfo
{
	public VerboseTransactionInfo(TransactionBlockInfo blockInfo, uint256 id, IEnumerable<VerboseInputInfo> inputs, IEnumerable<VerboseOutputInfo> outputs)
	{
		Id = id;
		BlockInfo = blockInfo;
		Inputs = inputs;
		Outputs = outputs;
	}

	public uint256 Id { get; }
	public TransactionBlockInfo BlockInfo { get; }
	public IEnumerable<VerboseInputInfo> Inputs { get; }

	public IEnumerable<VerboseOutputInfo> Outputs { get; }
}
