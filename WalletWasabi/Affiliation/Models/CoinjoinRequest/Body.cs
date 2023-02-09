using System.Collections.Generic;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Body(
	IEnumerable<Input> Inputs,
	IEnumerable<Output> Outputs,
	long Slip44CoinType,
	decimal FeeRate,
	long NoFeeThreshold,
	long MinRegistrableAmount,
	long Timestamp);
