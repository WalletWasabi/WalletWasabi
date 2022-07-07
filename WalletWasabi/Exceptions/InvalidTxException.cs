using NBitcoin;
using NBitcoin.Policy;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;

namespace WalletWasabi.Exceptions;

public class InvalidTxException : Exception
{
	public InvalidTxException(Transaction invalidTx, IEnumerable<TransactionPolicyError> errors)
	{
		Transaction = invalidTx;
		Errors = errors;
	}

	public Transaction Transaction { get; }
	public IEnumerable<TransactionPolicyError> Errors { get; }

	public override string Message
	{
		get
		{
			var errors = string.Join(Environment.NewLine, Errors.Select((error, i) => $"#{i}: {error}."));
			var txHex = string.Join(Environment.NewLine, Transaction.ToHex().ChunkBy(200).Select(x => new string(x.ToArray())));
			return string.Join(Environment.NewLine, "Invalid transaction:", txHex, "Policy errors:", errors);
		}
	}
}
