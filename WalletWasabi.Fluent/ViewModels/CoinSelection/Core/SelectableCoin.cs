using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public partial class SelectableCoin : ReactiveObject, ISelectable, ICoin
{
	private bool _isCoinjoining;
	private bool _isSelected;
	[AutoNotify] private Money _amount;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private int _anonymitySet;
	[AutoNotify] private DateTimeOffset? _bannedUntil;
	[AutoNotify] private ICoin _coin;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private OutPoint _outPoint;
	[AutoNotify] private PrivacyLevel _privacyLevel;
	[AutoNotify] private SmartLabel _smartLabel;

	public SelectableCoin(ICoin coin)
	{
		_coin = coin;
		_amount = Money.Zero;
		_amount = Money.Zero;
		_outPoint = OutPoint.Zero;
		_smartLabel = SmartLabel.Empty;
		
		this.WhenAnyValue(x => x.Coin.Amount).BindTo(this, x => x.Amount);
		this.WhenAnyValue(x => x.Coin.AnonymitySet).BindTo(this, x => x.AnonymitySet);
		this.WhenAnyValue(x => x.Coin.IsConfirmed).BindTo(this, x => x.IsConfirmed);
		this.WhenAnyValue(x => x.Coin.PrivacyLevel).BindTo(this, x => x.PrivacyLevel);
		this.WhenAnyValue(x => x.Coin.BannedUntil).BindTo(this, x => x.BannedUntil);
		this.WhenAnyValue(x => x.Coin.OutPoint).BindTo(this, x => x.OutPoint);
		this.WhenAnyValue(x => x.Coin.SmartLabel).BindTo(this, x => x.SmartLabel);
		this.WhenAnyValue(x => x.Coin.IsCoinjoining).BindTo(this, x => x.IsCoinjoining);
	}

	public bool IsCoinjoining
	{
		get => _isCoinjoining;
		set
		{
			this.RaiseAndSetIfChanged(ref _isCoinjoining, value);

			if (value)
			{
				IsSelected = false;
			}
		}
	}

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (IsCoinjoining)
			{
				value = false;
			}

			this.RaiseAndSetIfChanged(ref _isSelected, value);
		}
	}
}
