using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record InputsRemovalRequest(
	uint256 RoundId,
	Guid AliceId
);
