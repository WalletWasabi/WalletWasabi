using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;

public class PrivacyViewModel : ActivatableViewModel
{
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _balanceChanged;
	public IObservable<double>? PrivacyScore { get; private set; }

	public IObservable<bool>? HasPrivacyScore { get; private set; }

	public PrivacyViewModel(Wallet wallet, IObservable<Unit> balanceChanged)
	{
		_wallet = wallet;
		_balanceChanged = balanceChanged;
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		var privateScore = _balanceChanged
			.Select(_ => GetPrivateScore());

		PrivacyScore = privateScore
			.ObserveOn(RxApp.MainThreadScheduler);

		HasPrivacyScore = privateScore
			.Select(x => x > 0d)
			.ObserveOn(RxApp.MainThreadScheduler);
	}

	private double GetPrivateScore()
	{
		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;
		var currentPrivacyScore = _wallet.Coins.Sum(x =>
			x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		var maxPrivacyScore = _wallet.Coins.TotalAmount().Satoshi * (privateThreshold - 1);
		var privacyScore = maxPrivacyScore == 0 ? 1 : (double) currentPrivacyScore / maxPrivacyScore;
		return privacyScore;
	}
}