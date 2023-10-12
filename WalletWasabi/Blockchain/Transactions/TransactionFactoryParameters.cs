using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.Transactions;

public record TransactionFactoryParameters
{
	public TransactionFactoryParameters(Func<FeeRate> feeRateFetcher, bool allowUnconfirmed = false, bool allowDoubleSpend = false, IEnumerable<OutPoint>? allowedInputs = null, bool tryToSign = true)
	{
		FeeRateFetcher = feeRateFetcher;
		AllowUnconfirmed = allowUnconfirmed;
		AllowDoubleSpend = allowDoubleSpend;
		AllowedInputs = allowedInputs;
		TryToSign = tryToSign;
	}

	public Func<FeeRate> FeeRateFetcher { get; }
	public bool AllowUnconfirmed { get; }
	public bool AllowDoubleSpend { get; }
	public IEnumerable<OutPoint>? AllowedInputs { get; }
	public bool TryToSign { get; }
}
