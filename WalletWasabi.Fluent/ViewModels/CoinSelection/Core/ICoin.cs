using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public interface ICoin
{
	public DateTimeOffset? BannedUntil { get; }
	Money Amount { get; }
	int AnonymitySet { get; }
	SmartLabel SmartLabel { get; }
	PrivacyLevel PrivacyLevel { get; }
	bool IsConfirmed { get; }
	bool IsCoinjoining { get; }
	OutPoint OutPoint { get; }
}
