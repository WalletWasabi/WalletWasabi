using System.Collections.Generic;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Body(
	[property: CanonicalJsonIgnore] string TransactionId,
	IEnumerable<Input> Inputs,
	IEnumerable<Output> Outputs,
	long Slip44CoinType,
	CoordinatorFeeRate FeeRate,
	long NoFeeThreshold,
	long MinRegistrableAmount,
	long Timestamp);
