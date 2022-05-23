using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetailsViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _updateTrigger;

	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private int _blockHeight;
	[AutoNotify] private DateTimeOffset _date;
	[AutoNotify] private string? _amount;
	[AutoNotify] private SmartLabel? _labels;
	[AutoNotify] private string? _transactionId;
	[AutoNotify] private string? _blockHash;

	public TransactionDetailsViewModel(TransactionSummary transactionSummary, Wallet wallet, IObservable<Unit> updateTrigger)
	{
		_wallet = wallet;
		_updateTrigger = updateTrigger;

		NextCommand = ReactiveCommand.Create(OnNext);
		CopyTransactionIdCommand = ReactiveCommand.CreateFromTask(OnCopyTransactionIdAsync);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		UpdateValues(transactionSummary);
	}

	public ICommand CopyTransactionIdCommand { get; }

	private async Task OnCopyTransactionIdAsync()
	{
		if (TransactionId is null)
		{
			return;
		}

		if (Application.Current is { Clipboard: { } clipboard })
		{
			await clipboard.SetTextAsync(TransactionId);
		}
	}

	private void UpdateValues(TransactionSummary transactionSummary)
	{
		Date = transactionSummary.DateTime.ToLocalTime();
		TransactionId = transactionSummary.TransactionId.ToString();
		Labels = transactionSummary.Label;
		BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
		Confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int)_wallet.BitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
		IsConfirmed = Confirmations > 0;
		Amount = transactionSummary.Amount.ToString(fplus: false, trimExcessZero: false);
		BlockHash = transactionSummary.BlockHash?.ToString();
	}

	private void OnNext()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_updateTrigger
			.SubscribeAsync(async _ => await UpdateCurrentTransactionAsync())
			.DisposeWith(disposables);
	}

	private async Task UpdateCurrentTransactionAsync()
	{
		var historyBuilder = new TransactionHistoryBuilder(_wallet);
		var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

		var currentTransaction = txRecordList.FirstOrDefault(x => x.TransactionId.ToString() == TransactionId);

		if (currentTransaction is { })
		{
			UpdateValues(currentTransaction);
		}
	}
}
