using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Coinjoin Details", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinDetailsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly TransactionModel _transaction;

	[AutoNotify] private string _date = "";
	[AutoNotify] private Amount? _coinJoinFeeAmount;
	[AutoNotify] private uint256? _transactionId;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private bool _isConfirmationTimeVisible;
	[AutoNotify] private FeeRate? _feeRate;
	[AutoNotify] private bool _feeRateVisible;

	public CoinJoinDetailsViewModel(UiContext uiContext, IWalletModel wallet, TransactionModel transaction)
	{
		InputList = new CoinjoinCoinListViewModel(transaction.WalletInputs, wallet.Network, transaction.WalletInputs.Count + transaction.ForeignInputs.Value.Count);
		OutputList = new CoinjoinCoinListViewModel(transaction.WalletOutputs, wallet.Network, transaction.WalletOutputs.Count + transaction.ForeignOutputs.Value.Count);

		_wallet = wallet;
		_transaction = transaction;

		TransactionHex = transaction.Hex.Value;

		UiContext = uiContext;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
	}

	public CoinjoinCoinListViewModel InputList { get; }
	public CoinjoinCoinListViewModel OutputList { get; }
	public string TransactionHex { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Transactions.Cache
							.Connect()
							.SubscribeAsync(async _ => await UpdateAsync(CancellationToken.None))
							.DisposeWith(disposables);
	}

	private async Task UpdateAsync(CancellationToken cancellationToken)
	{
		if (_wallet.Transactions.TryGetById(_transaction.Id, _transaction.IsChild, out var transaction))
		{
			Date = transaction.DateToolTipString;
			CoinJoinFeeAmount = _wallet.AmountProvider.Create(Math.Abs(transaction.Amount));
			Confirmations = transaction.Confirmations;
			IsConfirmed = Confirmations > 0;
			TransactionId = transaction.Id;
			ConfirmationTime = await _wallet.Transactions.TryEstimateConfirmationTimeAsync(transaction.Id, cancellationToken);
			IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
			FeeRate = transaction.FeeRate;
			FeeRateVisible = FeeRate is not null && FeeRate != FeeRate.Zero;
		}
	}
}
