using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public partial class SmartCoinAdapter : ReactiveObject, ICoin
{
	[AutoNotify] private Money _amount;
	[AutoNotify] private int _anonymitySet;
	[AutoNotify] private DateTimeOffset? _bannedUntil;
	[AutoNotify] private bool _isCoinjoining;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private OutPoint _outPoint;
	[AutoNotify] private PrivacyLevel _privacyLevel;
	[AutoNotify] private SmartLabel _smartLabel;

	public SmartCoinAdapter(SmartCoin coin, int anonScoreTarget)
	{
		Coin = coin;
		this.WhenAnyValue(x => x.Coin.BannedUntilUtc).Do(x => BannedUntil = x).Subscribe();
		this.WhenAnyValue(x => x.Coin.Confirmed).Do(x => IsConfirmed = x).Subscribe();
		this.WhenAnyValue(x => x.Coin.CoinJoinInProgress).Do(x => IsCoinjoining = x).Subscribe();
		this.WhenAnyValue(x => x.Coin.Amount).Do(x => Amount = x).Subscribe();
		this.WhenAnyValue(x => x.Coin.HdPubKey.Cluster.Labels).Do(x => SmartLabel = x).Subscribe();
		this.WhenAnyValue(x => x.Coin.HdPubKey.AnonymitySet).Do(x => AnonymitySet = (int) x).Subscribe();
		this.WhenAnyValue(x => x.Coin.OutPoint).Do(x => OutPoint = x).Subscribe();

		_privacyLevel = GetPrivacyLevel(anonScoreTarget);
	}

	public SmartCoin Coin { get; }

	private PrivacyLevel GetPrivacyLevel(int anonScoreTarget)
	{
		if (Coin.IsPrivate(anonScoreTarget))
		{
			return PrivacyLevel.Private;
		}

		if (Coin.IsSemiPrivate())
		{
			return PrivacyLevel.SemiPrivate;
		}

		return PrivacyLevel.NonPrivate;
	}
}
