using WalletWasabi.Affiliation.Models;
namespace WalletWasabi.WabiSabi.Models;

public record RoundStateResponse(RoundState[] RoundStates, CoinJoinFeeRateMedian[] CoinJoinFeeRateMedians, AffiliateInformation AffiliateInformation);
