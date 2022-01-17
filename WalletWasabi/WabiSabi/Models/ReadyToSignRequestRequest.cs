using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.PostRequests;

public record ReadyToSignRequestRequest(uint256 RoundId, Guid AliceId);
