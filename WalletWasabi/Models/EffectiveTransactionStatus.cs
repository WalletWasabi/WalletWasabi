using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Models;

public record Ancestor
{
	public uint256 txid { get; set; }
	public long fee { get; set; }
	public long weight { get; set; }
}

public record EffectiveTransactionStatus
{
	public List<Ancestor> ancestors { get; set; }
	public double effectiveFeePerVsize { get; set; }
	public double adjustedVsize { get; set; }
}

