using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class RealWallete
{
	private readonly IObservable<Unit> _anonScoreChanged;
	private readonly Wallet _wallet;

	public RealWallete(Wallet wallet, IObservable<Unit> anonScoreChanged)
	{
		HistoryBuilder = new TransactionHistoryBuilder(wallet);
		_wallet = wallet;
		_anonScoreChanged = anonScoreChanged;
		SomethingChanged = GetSomethingChanged();
	}

	public TransactionHistoryBuilder HistoryBuilder { get; }

	public RealTransaction GetSummary(uint256 id)
	{
		return new RealTransaction(this, id);
	}

	public IObservable<Unit> SomethingChanged { get; }

	private IObservable<Unit> GetSomethingChanged()
	{
		return
			Observable.FromEventPattern(
					_wallet.TransactionProcessor,
					nameof(_wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.Select(_ => Unit.Default)
				.Merge(
					Observable.FromEventPattern(_wallet, nameof(_wallet.NewFilterProcessed))
						.Select(_ => Unit.Default))
				.Merge(Services.UiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.Merge(
					_anonScoreChanged.Select(_ => Unit.Default).Skip(1).Throttle(TimeSpan.FromMilliseconds(3000))
						.Throttle(TimeSpan.FromSeconds(0.1)));
	}
}
