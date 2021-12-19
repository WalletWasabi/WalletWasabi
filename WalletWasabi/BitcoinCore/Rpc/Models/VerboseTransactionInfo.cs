using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.BitcoinCore.Rpc.Models;

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

	public bool IsCoinbase => Inputs.Any(x => x.IsCoinbase);

	/// <summary>
	/// Note it can be negative if the transaction is coinbase.
	/// </summary>
	public Money NetworkFee => Inputs.Sum(x => x.PrevOutput?.Value ?? Money.Zero) - Outputs.Sum(x => x.Value);
}
