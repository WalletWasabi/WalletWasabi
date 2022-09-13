namespace WalletWasabi.Exceptions;

public class TransactionFeeOverpaymentException : Exception
{
	public TransactionFeeOverpaymentException(decimal percentageOfOverpayment)
		: base($"The transaction fee is more than the sent amount: {percentageOfOverpayment:0.#}%.")
	{
		PercentageOfOverpayment = percentageOfOverpayment;
	}

	public decimal PercentageOfOverpayment { get; }
}
