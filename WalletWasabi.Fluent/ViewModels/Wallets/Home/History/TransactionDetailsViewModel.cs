using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "Transaction Details")]
	public partial class TransactionDetailsViewModel : RoutableViewModel
	{
		private readonly BitcoinStore _bitcoinStore;
		private readonly Wallet _wallet;

		[AutoNotify] private bool _isConfirmed;
		[AutoNotify] private int _confirmations;
		[AutoNotify] private int _blockHeight;
		[AutoNotify] private DateTimeOffset _date;
		[AutoNotify] private string? _amount;
		[AutoNotify] private SmartLabel? _labels;
		[AutoNotify] private uint256? _transactionId;
		[AutoNotify] private DateTimeOffset _lastUpdated;

		public TransactionDetailsViewModel(TransactionSummary transactionSummary, BitcoinStore bitcoinStore, Wallet wallet)
		{
			_bitcoinStore = bitcoinStore;
			_wallet = wallet;

			NextCommand = ReactiveCommand.Create(OnNext);
			CopyTransactionIdCommand = ReactiveCommand.CreateFromTask(OnCopyTransactionId);

			UpdateValues(transactionSummary);
		}

		public ICommand CopyTransactionIdCommand { get; set; }

		private async Task OnCopyTransactionId()
		{
			if (TransactionId is null)
			{
				return;
			}

			await Application.Current.Clipboard.SetTextAsync(TransactionId.ToString());
		}

		private void UpdateValues(TransactionSummary transactionSummary)
		{
			Date = transactionSummary.DateTime.ToLocalTime();
			TransactionId = transactionSummary.TransactionId;
			Labels = transactionSummary.Label;
			BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
			Confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) _bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = Confirmations > 0;
			Amount = transactionSummary.Amount.ToString(fplus: false);

			LastUpdated = DateTimeOffset.Now.ToLocalTime();
		}

		private void OnNext()
		{
			Navigate().Clear();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Observable.FromEventPattern(_wallet, nameof(_wallet.NewFilterProcessed))
				.Merge(Observable.FromEventPattern(_wallet.TransactionProcessor, nameof(_wallet.TransactionProcessor.WalletRelevantTransactionProcessed)))
				.Throttle(TimeSpan.FromSeconds(3))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await UpdateCurrentTransactionAsync())
				.DisposeWith(disposables);
		}

		private async Task UpdateCurrentTransactionAsync()
		{
			var historyBuilder = new TransactionHistoryBuilder(_wallet);
			var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

			var currentTransaction = txRecordList.FirstOrDefault(x => x.TransactionId == TransactionId);

			if (currentTransaction is { })
			{
				UpdateValues(currentTransaction);
			}
		}
	}
}
