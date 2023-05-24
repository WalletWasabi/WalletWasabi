using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetailsViewModel : RoutableViewModel
{
	private readonly WalletViewModel _walletVm;

	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private int _blockHeight;
	[AutoNotify] private string _dateString;
	[AutoNotify] private Money? _amount;
	[AutoNotify] private SmartLabel? _labels;
	[AutoNotify] private string? _transactionId;
	[AutoNotify] private string? _blockHash;
	[AutoNotify] private string? _amountText = "";

	public TransactionDetailsViewModel(TransactionSummary transactionSummary, WalletViewModel walletVm)
	{
		_walletVm = walletVm;

		NextCommand = ReactiveCommand.Create(OnNext);

		Fee = transactionSummary.Fee;
		IsFeeVisible = transactionSummary.Fee != null && transactionSummary.Amount < 0;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		UpdateValues(transactionSummary);
	}

	public bool IsFeeVisible { get; set; }

	public Money? Fee { get; set; }

	private void UpdateValues(TransactionSummary transactionSummary)
	{
		DateString = transactionSummary.DateTime.ToLocalTime().ToUserFacingString();
		TransactionId = transactionSummary.TransactionId.ToString();
		Labels = transactionSummary.Label;
		BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
		Confirmations = transactionSummary.GetConfirmations();
		IsConfirmed = Confirmations > 0;
		Amount = transactionSummary.Amount.Abs();
		AmountText = transactionSummary.Amount < Money.Zero ? "Outgoing" : "Incoming";
		BlockHash = transactionSummary.BlockHash?.ToString();
	}

	private void OnNext()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_walletVm.UiTriggers.TransactionsUpdateTrigger
			.DoAsync(async _ => await UpdateCurrentTransactionAsync())
			.Subscribe()
			.DisposeWith(disposables);
	}

	private async Task UpdateCurrentTransactionAsync()
	{
		var historyBuilder = new TransactionHistoryBuilder(_walletVm.Wallet);
		var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

		var currentTransaction = txRecordList.FirstOrDefault(x => x.TransactionId.ToString() == TransactionId);

		if (currentTransaction is { })
		{
			UpdateValues(currentTransaction);
		}
	}
}
