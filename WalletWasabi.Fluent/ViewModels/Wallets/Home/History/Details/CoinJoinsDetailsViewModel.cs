using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinsDetailsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly TransactionModel _transaction;

	[AutoNotify] private string _date = "";
	[AutoNotify] private string _status = "";
	[AutoNotify] private string _coinJoinFeeRawString = "";
	[AutoNotify] private string _coinJoinFeeString = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private uint256? _transactionId;
	[AutoNotify] private ObservableCollection<uint256>? _transactionIds;
	[AutoNotify] private int _txCount;

	public CoinJoinsDetailsViewModel(UiContext uiContext, IWalletModel wallet, TransactionModel transaction)
	{
		_wallet = wallet;
		_transaction = transaction;
		var allWalletInputs = transaction.WalletInputs.Union(transaction.Children.SelectMany(x => x.WalletInputs)).ToList();
		var allWalletOutputs = transaction.WalletOutputs.Union(transaction.Children.SelectMany(x => x.WalletOutputs)).ToList();
		var freshWalletInputs = allWalletInputs.Where(x => !allWalletOutputs.Select(y => y.Outpoint).Contains(x.Outpoint)).OrderByDescending(x => x.Amount).ToList();
		var finalWalletOutputs = allWalletOutputs.Where(x => !allWalletInputs.Select(y => y.Outpoint).Contains(x.Outpoint)).OrderByDescending(x => x.Amount).ToList();

		var allInputs = transaction.ForeignInputs.Value
			.Union(transaction.Children.SelectMany(x => x.ForeignInputs.Value))
			.Union(allWalletInputs.Select(x => x.Outpoint))
			.ToList();

		var allOutputs = transaction.ForeignOutputs.Value.Select(x => new OutPoint(x.Transaction.GetHash(), x.N))
			.Union(transaction.Children.SelectMany(x => x.ForeignOutputs.Value.Select(y => new OutPoint(y.Transaction.GetHash(), y.N))))
			.Union(allWalletOutputs.Select(x => x.Outpoint))
			.ToList();

		var freshInputs = allInputs.Where(x => !allOutputs.Contains(x));
		var finalOutputs = allOutputs.Where(x => !allInputs.Contains(x));

		InputList = new CoinjoinCoinListViewModel(freshWalletInputs, wallet.Network, freshWalletInputs.Count + freshInputs.Count());
		OutputList = new CoinjoinCoinListViewModel(finalWalletOutputs, wallet.Network, finalWalletOutputs.Count + finalOutputs.Count());

		UiContext = uiContext;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;

		ConfirmationTime = Task.Run(() => wallet.Transactions.TryEstimateConfirmationTimeAsync(transaction, CancellationToken.None)).Result;
		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
	}

	public CoinjoinCoinListViewModel InputList { get; }
	public CoinjoinCoinListViewModel OutputList { get; }

	public TimeSpan? ConfirmationTime { get; set; }

	public bool IsConfirmationTimeVisible { get; set; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Transactions.Cache
			                .Connect()
							.Do(_ => Update())
							.Subscribe()
							.DisposeWith(disposables);
	}

	private void Update()
	{
		if (_wallet.Transactions.TryGetById(_transaction.Id, _transaction.IsChild, out var transaction))
		{
			Date = transaction.DateToolTipString;
			Status = transaction.IsConfirmed ? "Confirmed" : "Pending";
			CoinJoinFeeAmount = _wallet.AmountProvider.Create(Math.Abs(transaction.Amount));
			TransactionId = transaction.Id;
			TransactionIds = new ObservableCollection<uint256>(transaction.Children.Select(x => x.Id));
			TxCount = TransactionIds.Count;
		}
	}
}
