using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetailsViewModel : RoutableViewModel
{
	private readonly WalletViewModel _walletVm;

	[ObservableProperty] private bool _isConfirmed;
	[ObservableProperty] private int _confirmations;
	[ObservableProperty] private int _blockHeight;
	[ObservableProperty] private DateTimeOffset _date;
	[ObservableProperty] private string? _amount;
	[ObservableProperty] private SmartLabel? _labels;
	[ObservableProperty] private string? _transactionId;
	[ObservableProperty] private string? _blockHash;

	public TransactionDetailsViewModel(TransactionSummary transactionSummary, WalletViewModel walletVm)
	{
		_walletVm = walletVm;

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
		Confirmations = transactionSummary.GetConfirmations();
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
