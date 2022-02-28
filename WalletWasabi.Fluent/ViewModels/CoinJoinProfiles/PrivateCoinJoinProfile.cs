using Avalonia.Media;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

internal class PrivateCoinJoinProfile : CoinJoinProfileViewModelBase
{
	public override string Title => "Maximize Privacy";

	public override string Description => "Choice of the paranoid. Optimizes for privacy at all costs.";

	public override IImage Icon => AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/CoinJoinProfiles/private.png");

	public override int MinAnonScoreTarget { get; } = GetRandom(40, 61);

	public override int MaxAnonScoreTarget { get; } = GetRandom(90, 111);

	public override int FeeRateMedianTimeFrameHours => 0;

	private static int GetRandom(int minInclusive, int maxExclusive)
	{
		using SecureRandom rand = new();
		return rand.GetInt(minInclusive, maxExclusive);
	}
}
