using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WasabiDocumentTabViewModel, IWalletViewModel
	{
		private ObservableCollection<TransactionViewModel> _transactions;
		private TransactionViewModel _selectedTransaction;
		private SortOrder _dateSortDirection;
		private SortOrder _amountSortDirection;
		private SortOrder _transactionSortDirection;

		public HistoryTabViewModel(Wallet wallet)
			: base("History")
		{
			Global = Locator.Current.GetService<Global>();
			Wallet = wallet;

			Transactions = new ObservableCollection<TransactionViewModel>();

			SortCommand = ReactiveCommand.Create(RefreshOrdering);
			DateSortDirection = SortOrder.Decreasing;

			SortCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});			
		}

		private Global Global { get; }

		private Wallet Wallet { get; }

		Wallet IWalletViewModel.Wallet => Wallet;

		public ReactiveCommand<Unit, Unit> SortCommand { get; }

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);

			Observable.FromEventPattern(Wallet, nameof(Wallet.NewBlockProcessed))
				.Merge(Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)))
				.Throttle(TimeSpan.FromSeconds(3))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await TryRewriteTableAsync())
				.DisposeWith(disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
				{
					foreach (var transaction in Transactions)
					{
						transaction.Refresh();
					}
				}).DisposeWith(disposables);

			_ = TryRewriteTableAsync();
		}

		private async Task TryRewriteTableAsync()
		{
			try
			{
				var historyBuilder = new TransactionHistoryBuilder(Wallet);
				var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

				var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
				Transactions?.Clear();

				var trs = txRecordList.Select(txr => new TransactionInfo
				{
					DateTime = txr.DateTime.ToLocalTime(),
					Confirmed = txr.Height.Type == HeightType.Chain,
					Confirmations = txr.Height.Type == HeightType.Chain ? (int)Global.BitcoinStore.SmartHeaderChain.TipHeight - txr.Height.Value + 1 : 0,
					AmountBtc = $"{txr.Amount.ToString(fplus: true, trimExcessZero: true)}",
					Label = txr.Label,
					BlockHeight = txr.Height.Type == HeightType.Chain ? txr.Height.Value : 0,
					TransactionId = txr.TransactionId.ToString()
				}).Select(ti => new TransactionViewModel(ti));

				Transactions = new ObservableCollection<TransactionViewModel>(trs);

				if (Transactions.Count > 0 && !(rememberSelectedTransactionId is null))
				{
					var txToSelect = Transactions.FirstOrDefault(x => x.TransactionId == rememberSelectedTransactionId);
					if (txToSelect != null)
					{
						SelectedTransaction = txToSelect;
					}
				}
				RefreshOrdering();
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		public ObservableCollection<TransactionViewModel> Transactions
		{
			get => _transactions;
			set => this.RaiseAndSetIfChanged(ref _transactions, value);
		}

		public TransactionViewModel SelectedTransaction
		{
			get => _selectedTransaction;
			set => this.RaiseAndSetIfChanged(ref _selectedTransaction, value);
		}

		public SortOrder DateSortDirection
		{
			get => _dateSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _dateSortDirection, value);
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					TransactionSortDirection = SortOrder.None;
				}
			}
		}

		public SortOrder AmountSortDirection
		{
			get => _amountSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _amountSortDirection, value);
				if (value != SortOrder.None)
				{
					DateSortDirection = SortOrder.None;
					TransactionSortDirection = SortOrder.None;
				}
			}
		}

		public SortOrder TransactionSortDirection
		{
			get => _transactionSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _transactionSortDirection, value);
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					DateSortDirection = SortOrder.None;
				}
			}
		}

		private void RefreshOrdering()
		{
			if (TransactionSortDirection != SortOrder.None)
			{
				switch (TransactionSortDirection)
				{
					case SortOrder.Increasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderBy(t => t.TransactionId));
						break;

					case SortOrder.Decreasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderByDescending(t => t.TransactionId));
						break;
				}
			}
			else if (AmountSortDirection != SortOrder.None)
			{
				switch (AmountSortDirection)
				{
					case SortOrder.Increasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderBy(t => t.Amount));
						break;

					case SortOrder.Decreasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderByDescending(t => t.Amount));
						break;
				}
			}
			else if (DateSortDirection != SortOrder.None)
			{
				switch (DateSortDirection)
				{
					case SortOrder.Increasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderBy(t => t.DateTime));
						break;

					case SortOrder.Decreasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderByDescending(t => t.DateTime));
						break;
				}
			}
		}
	}
}
