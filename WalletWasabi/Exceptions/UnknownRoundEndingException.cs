using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Exceptions;

public class UnknownRoundEndingException : Exception
{
	public ImmutableList<SmartCoin> Coins { get; }
	public ImmutableList<Script> OutputScripts { get; }

	public UnknownRoundEndingException(ImmutableList<SmartCoin> coins, ImmutableList<Script> outputScripts, Exception exception) :
		base(
			$"Round was not ended properly, reason '{exception.Message}'.",
			innerException: exception)
	{
		Coins = coins;
		OutputScripts = outputScripts;
	}
}
