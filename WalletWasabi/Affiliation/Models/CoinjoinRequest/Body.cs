using System.Collections.Generic;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Body(
	IEnumerable<Input> Inputs,
	IEnumerable<Output> Outputs,
	long Slip44CoinType,
	CoordinatorFeeRate FeeRate,
	long NoFeeThreshold,
	long MinRegistrableAmount,
	long Timestamp);
