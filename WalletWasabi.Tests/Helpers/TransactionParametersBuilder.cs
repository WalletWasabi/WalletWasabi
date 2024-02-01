using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Tests.Helpers;

public class TransactionParametersBuilder
{
	private TransactionParametersBuilder(
		PaymentIntent? payment = null,
		FeeRate? feeRate = null,
		bool allowUnconfirmed = false,
		bool allowDoubleSpend = false,
		IEnumerable<OutPoint>? allowedInputs = null,
		bool tryToSign = true,
		bool overrideFeeProtection = false)
	{
		Payment = payment;
		FeeRate = feeRate;
		AllowUnconfirmed = allowUnconfirmed;
		AllowDoubleSpend = allowDoubleSpend;
		AllowedInputs = allowedInputs;
		TryToSign = tryToSign;
		OverrideFeeProtection = overrideFeeProtection;
	}

	private PaymentIntent? Payment { get; set; }
	private FeeRate? FeeRate { get; set; }
	private bool AllowUnconfirmed { get; set; }
	private bool AllowDoubleSpend { get; set; }
	private IEnumerable<OutPoint>? AllowedInputs { get; set; }
	private bool TryToSign { get; set; }
	private bool OverrideFeeProtection { get; set; }

	public TransactionParametersBuilder SetPayment(PaymentIntent payment)
	{
		Payment = payment;
		return this;
	}

	public TransactionParametersBuilder SetFeeRate(decimal satoshisPerByte)
	{
		FeeRate = new FeeRate(satoshisPerByte);
		return this;
	}

	public TransactionParametersBuilder SetAllowUnconfirmed(bool value)
	{
		AllowUnconfirmed = value;
		return this;
	}

	public TransactionParametersBuilder SetAllowDoubleSpend(bool value)
	{
		AllowDoubleSpend = value;
		return this;
	}

	public TransactionParametersBuilder SetAllowedInputs(IEnumerable<OutPoint>? inputs)
	{
		AllowedInputs = inputs;
		return this;
	}

	public TransactionParametersBuilder SetTryToSign(bool value)
	{
		TryToSign = value;
		return this;
	}

	public TransactionParametersBuilder SetOverrideFeeProtection(bool value)
	{
		OverrideFeeProtection = value;
		return this;
	}

	public TransactionParameters Build()
	{
		if (Payment is null)
		{
			throw new InvalidOperationException("Payment intent is a required parameter.");
		}

		if (FeeRate is null)
		{
			throw new InvalidOperationException("Fee rate is a required parameter.");
		}

		return new TransactionParameters(Payment, FeeRate, AllowUnconfirmed, AllowDoubleSpend, AllowedInputs, TryToSign, OverrideFeeProtection);
	}

	public static TransactionParametersBuilder CreateDefault()
		=> new();
}
