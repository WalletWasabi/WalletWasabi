using Avalonia.Media;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public abstract class CoinJoinProfileViewModel : ViewModelBase
{
	public abstract string Title { get; }
	public abstract string Description { get; }
	public abstract IImage Icon { get; }

	public virtual int MinAnonScoreTarget { get; } = 5;

	public virtual int MaxAnonScoreTarget { get; } = 10;
	public abstract int FeeTargetAvarageTimeFrameHours { get; }
}
