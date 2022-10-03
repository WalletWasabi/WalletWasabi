using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class AnonymityScoreCellViewModel : ViewModelBase
{
	public AnonymityScoreCellViewModel(ICoin coin)
	{
		Coin = coin;

		PrivacyScore = this.WhenAnyValue(x => x.Coin.AnonymitySet);

		IsPrivate = this.WhenAnyValue(x => x.Coin.PrivacyLevel, x => x == PrivacyLevel.Private);
		IsSemiPrivate = this.WhenAnyValue(x => x.Coin.PrivacyLevel, x => x == PrivacyLevel.SemiPrivate);
		IsNonPrivate = this.WhenAnyValue(x => x.Coin.PrivacyLevel, x => x == PrivacyLevel.NonPrivate);

		IsVisible = this.WhenAnyValue(x => x.Coin.OutPoint, point => point != OutPoint.Zero);
	}

	public IObservable<bool> IsNonPrivate { get; }

	public IObservable<bool> IsSemiPrivate { get; }

	public IObservable<bool> IsPrivate { get; }

	public IObservable<int> PrivacyScore { get; }

	public IObservable<bool> IsVisible { get; }

	private ICoin Coin { get; }
}
