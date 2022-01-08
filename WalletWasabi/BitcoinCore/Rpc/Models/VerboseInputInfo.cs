using NBitcoin;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.BitcoinCore.Rpc.Models;

public class VerboseInputInfo
{
	public VerboseInputInfo(string coinbase)
		: this(null, null, coinbase)
	{
	}

	public VerboseInputInfo(OutPoint outPoint, VerboseOutputInfo prevOutput)
		: this(outPoint, prevOutput, null)
	{
	}

	private VerboseInputInfo(OutPoint? outPoint, VerboseOutputInfo? prevOutput, string? coinbase)
	{
		OutPoint = outPoint;
		PrevOutput = prevOutput;
		Coinbase = coinbase;
	}

	public OutPoint? OutPoint { get; }

	public VerboseOutputInfo? PrevOutput { get; }

	public string? Coinbase { get; }

	[MemberNotNullWhen(returnValue: true, nameof(Coinbase))]
	public bool IsCoinbase => Coinbase is not null;
}
