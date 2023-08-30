namespace WalletWasabi.Exceptions;

public class TransactionFeeOverpaymentException : Exception
{
	public TransactionFeeOverpaymentException(decimal percentageOfOverpayment, decimal fee)
		: base($"The transaction fee is more than the sent amount: {percentageOfOverpayment:0.#}%.")
	{
		PercentageOfOverpayment = percentageOfOverpayment;
		Fee = fee;
	}

	public decimal PercentageOfOverpayment { get; }
	public decimal Fee { get; }
}
