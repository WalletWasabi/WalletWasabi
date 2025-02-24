using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record ReadyToSignRequestRequest(
	uint256 RoundId,
	Guid AliceId);
