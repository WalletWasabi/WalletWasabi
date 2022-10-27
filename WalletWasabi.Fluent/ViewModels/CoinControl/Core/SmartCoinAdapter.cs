using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class SmartCoinAdapter : ICoin
{
	private readonly SmartCoin _smartCoin;

	public SmartCoinAdapter(SmartCoin smartCoin)
	{
		_smartCoin = smartCoin;
	}

	public Money Amount => _smartCoin.Amount;
	public int AnonymityScore => (int) _smartCoin.HdPubKey.AnonymitySet;
	public SmartLabel Labels => _smartCoin.HdPubKey.Label;
	public bool IsCoinjoining => _smartCoin.CoinJoinInProgress;
	public DateTimeOffset? BannedUntil => _smartCoin.BannedUntilUtc;
	public bool IsConfirmed => _smartCoin.Confirmed;
}
