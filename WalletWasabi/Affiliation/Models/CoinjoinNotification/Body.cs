using System.Collections.Generic;

namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Body(
	IEnumerable<Input> Inputs,
	IEnumerable<Output> Outputs,
	long Slip44CoinType,
	CoordinatorFeeRate FeeRate,
	long NoFeeThreshold,
	long MinRegistrableAmount,
	long Timestamp);
