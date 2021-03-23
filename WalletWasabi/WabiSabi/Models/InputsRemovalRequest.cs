using System;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputsRemovalRequest(
		Guid RoundId,
		Guid AliceId
	);
}
