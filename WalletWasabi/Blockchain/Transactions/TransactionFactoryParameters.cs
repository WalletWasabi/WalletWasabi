using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.Transactions;

public record TransactionFactoryParameters(Func<FeeRate> FeeRateFetcher, bool AllowUnconfirmed = false, bool AllowDoubleSpend = false, IEnumerable<OutPoint>? AllowedInputs = null, bool TryToSign = true);
