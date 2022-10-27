using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class PocketCoinAdapter : ICoin
{
	private readonly Pocket _pocket;

	public PocketCoinAdapter(Pocket pocket)
	{
		_pocket = pocket;
	}

	public Money Amount => _pocket.Amount;
	public int AnonymityScore => _pocket.Coins.Max(x => (int) x.HdPubKey.AnonymitySet);
	public SmartLabel Labels => _pocket.Labels;
	public bool IsCoinjoining => _pocket.Coins.Any(x => x.CoinJoinInProgress);
	public DateTimeOffset? BannedUntil => _pocket.Coins.Max(x => x.BannedUntilUtc);
	public bool IsConfirmed => _pocket.Coins.All(x => x.Confirmed);
}
