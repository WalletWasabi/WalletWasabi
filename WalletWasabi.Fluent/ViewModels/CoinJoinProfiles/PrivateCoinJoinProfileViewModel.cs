using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class PrivateCoinJoinProfileViewModel : CoinJoinProfileViewModelBase
{
	public override string Title => "Maximize Privacy";

	public override string Description => "Choice of the paranoid. Optimizes for privacy at all costs.";

	public override int AnonScoreTarget { get; } = GetRandom(50, 101);

	public override int FeeRateMedianTimeFrameHours => 0;

	private static int GetRandom(int minInclusive, int maxExclusive)
	{
		return SecureRandom.Instance.GetInt(minInclusive, maxExclusive);
	}
}
