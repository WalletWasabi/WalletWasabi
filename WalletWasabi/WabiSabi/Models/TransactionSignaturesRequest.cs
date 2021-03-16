using System;
using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Models
{
	public record TransactionSignaturesRequest(
		Guid RoundId,
		IEnumerable<InputWitnessPair> InputWitnessPairs
	);
}
