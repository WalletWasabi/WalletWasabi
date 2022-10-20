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

		Amount = coin.Amount;

		this.WhenAnyValue(x => x.Coin.BannedUntilUtc).BindTo(this, x => x.BannedUntil);
		this.WhenAnyValue(x => x.Coin.Confirmed).BindTo(this, x => x.IsConfirmed);
		this.WhenAnyValue(x => x.Coin.CoinJoinInProgress).BindTo(this, x => x.IsCoinjoining);
		this.WhenAnyValue(x => x.Coin.HdPubKey.Cluster.Labels).BindTo(this, x => x.SmartLabel);
		this.WhenAnyValue(x => x.Coin.HdPubKey.AnonymitySet).Do(x => AnonymitySet = (int) x).Subscribe();
		this.WhenAnyValue(x => x.Coin.OutPoint).BindTo(this, x => x.OutPoint);

		_privacyLevel = GetPrivacyLevel(anonScoreTarget);
	}

	private SmartCoin Coin { get; }

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
