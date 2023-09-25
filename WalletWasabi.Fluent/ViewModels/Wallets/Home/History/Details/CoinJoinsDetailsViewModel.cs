using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoins")]
public partial class CoinJoinsDetailsViewModel : RoutableViewModel
{
	private readonly CoinJoinsHistoryItemViewModel _coinJoinGroup;
	private readonly IObservable<Unit> _updateTrigger;

	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";
	[AutoNotify] private string _coinJoinFeeRawString = "";
	[AutoNotify] private string _coinJoinFeeString = "";
	[AutoNotify] private ObservableCollection<uint256>? _transactionIds;
	[AutoNotify] private int _txCount;

	public CoinJoinsDetailsViewModel(CoinJoinsHistoryItemViewModel coinJoinGroup, IObservable<Unit> updateTrigger)
	{
		_coinJoinGroup = coinJoinGroup;
		_updateTrigger = updateTrigger;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;

		CopyCommand = ReactiveCommand.CreateFromTask<uint256>(async txid =>
		{
			if (ApplicationHelper.Clipboard is { } clipboard)
			{
				await clipboard.SetTextAsync(txid.ToString());
			}
		});

		Update();

		ConfirmationTime = TimeSpan.Zero; // TODO: Calculate confirmation time
		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
	}

	public TimeSpan? ConfirmationTime { get; set; }

	public bool IsConfirmationTimeVisible { get; set; }

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
		Date = _coinJoinGroup.DateString;
		Status = _coinJoinGroup.IsConfirmed ? "Confirmed" : "Pending";
		CoinJoinFeeRawString = _coinJoinGroup.OutgoingAmount.ToFeeDisplayUnitRawString();
		CoinJoinFeeString = _coinJoinGroup.OutgoingAmount.ToFeeDisplayUnitFormattedString();

		TransactionIds = new ObservableCollection<uint256>(_coinJoinGroup.CoinJoinTransactions.Select(x => x.GetHash()));
		TxCount = TransactionIds.Count;
	}
}
