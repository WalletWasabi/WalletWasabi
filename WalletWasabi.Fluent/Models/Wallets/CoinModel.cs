using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class CoinModel : ReactiveObject
{
	[AutoNotify] private bool _isExcludedFromCoinJoin;
	[AutoNotify] private bool _isCoinJoinInProgress;
	[AutoNotify] private bool _isBanned;
	[AutoNotify] private string? _bannedUntilUtcToolTip;
	[AutoNotify] private string? _confirmedToolTip;
	[AutoNotify] private int _anonScore;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private bool _isConfirmed;

	public CoinModel(SmartCoin coin, int anonScoreTarget)
	{
		Coin = coin;
		PrivacyLevel = coin.GetPrivacyLevel(anonScoreTarget);
		Amount = coin.Amount;

		Labels = coin.GetLabels(anonScoreTarget);
		Key = coin.Outpoint.GetHashCode();
		BannedUntilUtc = coin.BannedUntilUtc;
		ScriptType = ScriptType.FromEnum(coin.ScriptType);

		this.WhenAnyValue(c => c.Coin.IsExcludedFromCoinJoin).BindTo(this, x => x.IsExcludedFromCoinJoin);
		this.WhenAnyValue(c => c.Coin.Confirmed).BindTo(this, x => x.IsConfirmed);
		this.WhenAnyValue(c => c.Coin.HdPubKey.AnonymitySet).Select(x => (int)x).BindTo(this, x => x.AnonScore);
		this.WhenAnyValue(c => c.Coin.CoinJoinInProgress).BindTo(this, x => x.IsCoinJoinInProgress);
		this.WhenAnyValue(c => c.Coin.IsBanned).BindTo(this, x => x.IsBanned);
		this.WhenAnyValue(c => c.Coin.BannedUntilUtc).WhereNotNull().Subscribe(x => BannedUntilUtcToolTip = $"Can't participate in coinjoin until: {x:g}");

		this.WhenAnyValue(c => c.Coin.Height).Select(_ => Coin.GetConfirmations()).Subscribe(x =>
		{
			Confirmations = x;
			ConfirmedToolTip = $"{x} confirmation{TextHelpers.AddSIfPlural(x)}";
		});
	}

	internal SmartCoin Coin { get; }

	public Money Amount { get; }

	public int Key { get; }

	public PrivacyLevel PrivacyLevel { get; }

	public LabelsArray Labels { get; }

	public ScriptType ScriptType { get; }

	public DateTimeOffset? BannedUntilUtc { get; }

	public bool IsPrivate => PrivacyLevel == PrivacyLevel.Private;

	public bool IsSemiPrivate => PrivacyLevel == PrivacyLevel.SemiPrivate;

	public bool IsNonPrivate => PrivacyLevel == PrivacyLevel.NonPrivate;

	public bool IsSameAddress(ICoinModel anotherCoin) => anotherCoin is CoinModel cm && cm.Coin.HdPubKey == Coin.HdPubKey;

	// TODO: Leaky abstraction. This shouldn't exist.
	public SmartCoin GetSmartCoin() => Coin;
}
