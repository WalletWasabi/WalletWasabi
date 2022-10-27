using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public interface ICoin
{
	Money Amount { get; }
	int AnonymityScore { get; }
	SmartLabel Labels { get; }
	bool IsCoinjoining { get; }
	DateTimeOffset? BannedUntil { get; }
	bool IsConfirmed { get; }
}
