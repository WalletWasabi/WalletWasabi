using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using WalletWasabi.Helpers;

namespace WalletWasabi.Exceptions
{
	public class InvalidTxException : Exception
	{
		public Transaction Transaction { get; }
		public IEnumerable<TransactionPolicyError> Errors { get; }

		public InvalidTxException(Transaction invalidTx, IEnumerable<TransactionPolicyError> errors = null)
		{
			Transaction = Guard.NotNull(nameof(invalidTx), invalidTx);
			Errors = errors;
		}
	}
}
