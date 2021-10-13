using System;

namespace WalletWasabi.Exceptions
{
	public class TransactionFeeOverpaymentException : Exception
	{
		public TransactionFeeOverpaymentException(decimal percentageOfOverpayment)
			: base($"The transaction fee is more than twice the sent amount: {percentageOfOverpayment:0.#}%.")
		{
			PercentageOfOverpayment = percentageOfOverpayment;
		}

		public decimal PercentageOfOverpayment { get; }
	}
}
