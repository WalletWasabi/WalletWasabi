using System;
using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Models
{
	public record TransactionSignaturesRequest(
		uint256 RoundId,
		IEnumerable<InputWitnessPair> InputWitnessPairs
	);
}
