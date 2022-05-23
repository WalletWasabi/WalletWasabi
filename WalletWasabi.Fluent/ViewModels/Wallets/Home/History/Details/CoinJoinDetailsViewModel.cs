using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoin Details")]
public partial class CoinJoinDetailsViewModel : RoutableViewModel
{
	private readonly CoinJoinHistoryItemViewModel _coinJoin;
	private readonly IObservable<Unit> _updateTrigger;

	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";
	[AutoNotify] private Money? _coinJoinFee;
	[AutoNotify] private string _coinJoinFeeString = "";
	[AutoNotify] private uint256? _transactionId;

	public CoinJoinDetailsViewModel(CoinJoinHistoryItemViewModel coinJoin, IObservable<Unit> updateTrigger)
	{
		_coinJoin = coinJoin;
		_updateTrigger = updateTrigger;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;

		CopyCommand = ReactiveCommand.CreateFromTask<uint256>(async txid =>
		{
			if (Application.Current is { Clipboard: { } clipboard })
			{
				await clipboard.SetTextAsync(txid.ToString());
			}
		});

		Update();
	}

	public ICommand CopyCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_updateTrigger
			.Subscribe(_ => Update())
			.DisposeWith(disposables);
	}

	private void Update()
	{
		Date = _coinJoin.DateString;
		Status = _coinJoin.IsConfirmed ? "Confirmed" : "Pending";
		CoinJoinFee = _coinJoin.OutgoingAmount;
		CoinJoinFeeString = CoinJoinFee.ToFeeDisplayUnitString() ?? "Unknown";

		TransactionId = _coinJoin.CoinJoinTransaction.TransactionId;
	}
}
