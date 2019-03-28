using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<TransactionViewModel> _transactions;
		private TransactionViewModel _selectedTransaction;
		private SortOrder _dateSortDirection;
		private SortOrder _amountSortDirection;
		private SortOrder _transactionSortDirection;
		private CompositeDisposable _disposables;

		public ReactiveCommand SortCommand { get; }
		public ReactiveCommand CoinJoinTransactionVisibleCommand { get; }

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			Transactions = new ObservableCollection<TransactionViewModel>();

			RewriteTableAsync().GetAwaiter();

			this.WhenAnyValue(x => x.SelectedTransaction).Subscribe(transaction =>
			{
				if (Global.UiConfig.Autocopy is true)
				{
					transaction?.CopyToClipboard();
				}
			});

			SortCommand = ReactiveCommand.Create(() => RefreshOrdering());

			CoinJoinTransactionVisibleCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				Global.UiConfig.CoinJoinTransactionVisible = !Global.UiConfig.CoinJoinTransactionVisible;
				await Global.UiConfig.ToFileAsync();
			});

			DateSortDirection = SortOrder.Decreasing;
		}

		public override void OnOpen()
		{
			base.OnOpen();

			if (_disposables != null)
			{
				throw new Exception("History Tab was opened before it was closed.");
			}

			_disposables = new CompositeDisposable();

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.NewBlockProcessed)))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed)))
				.Throttle(TimeSpan.FromSeconds(5))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await RewriteTableAsync())
				.DisposeWith(_disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(x =>
			{
				foreach (var transaction in Transactions)
				{
					transaction.Refresh();
				}
			}).DisposeWith(_disposables);

			Global.UiConfig.WhenAnyValue(x => x.CoinJoinTransactionVisible).ObserveOn(RxApp.MainThreadScheduler).Subscribe(async x =>
			{
				await RewriteTableAsync();
			}).DisposeWith(_disposables);
		}

		public override bool OnClose()
		{
			_disposables.Dispose();
			_disposables = null;

			return base.OnClose();
		}

		private async Task RewriteTableAsync()
		{
			var txRecordList = await Task.Run(() =>
			{
				return BuildTxRecordList();
			});

			var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
			Transactions?.Clear();

			var trs = txRecordList.Select(txr => new TransactionInfo
			{
				DateTime = txr.dateTime.ToLocalTime(),
				Confirmed = txr.height != WalletWasabi.Models.Height.MemPool && txr.height != WalletWasabi.Models.Height.Unknown,
				AmountBtc = $"{txr.amount.ToString(fplus: true, trimExcessZero: true)}",
				Label = txr.label,
				TransactionId = txr.transactionId.ToString()
			}).Select(ti => new TransactionViewModel(ti));

			Transactions = new ObservableCollection<TransactionViewModel>(trs.Where(tr => Global.UiConfig.CoinJoinTransactionVisible == true || !tr.IsCoinJoinTransaction));

			if (Transactions.Count > 0 && !(rememberSelectedTransactionId is null))
			{
				var txToSelect = Transactions.FirstOrDefault(x => x.TransactionId == rememberSelectedTransactionId);
				if (txToSelect != default)
				{
					SelectedTransaction = txToSelect;
				}
			}
			RefreshOrdering();
		}

		private static List<(DateTimeOffset dateTime, Height height, Money amount, string label, uint256 transactionId)> BuildTxRecordList()
		{
			List<Transaction> trs = new List<Transaction>();
			var txRecordList = new List<(DateTimeOffset dateTime, Height height, Money amount, string label, uint256 transactionId)>();
			foreach (SmartCoin coin in Global.WalletService.Coins)
			{
				var found = txRecordList.FirstOrDefault(x => x.transactionId == coin.TransactionId);

				if (Global.WalletService is null) // disposed meanwhile
				{
					break;
				}

				SmartTransaction foundTransaction = Global.WalletService.TransactionCache.First(x => x.GetHash() == coin.TransactionId);
				DateTimeOffset dateTime;
				if (foundTransaction.Height.Type == HeightType.Chain)
				{
					if (Global.WalletService.ProcessedBlocks.Any(x => x.Value.height == foundTransaction.Height))
					{
						dateTime = Global.WalletService.ProcessedBlocks.First(x => x.Value.height == foundTransaction.Height).Value.dateTime;
					}
					else
					{
						dateTime = DateTimeOffset.UtcNow;
					}
				}
				else
				{
					dateTime = foundTransaction.FirstSeenIfMemPoolTime ?? DateTimeOffset.UtcNow;
				}
				if (found != default) // if found
				{
					txRecordList.Remove(found);
					var foundLabel = found.label != string.Empty ? found.label + ", " : "";
					var newRecord = (dateTime, found.height, found.amount + coin.Amount, $"{foundLabel}{coin.Label}", coin.TransactionId);
					txRecordList.Add(newRecord);
				}
				else
				{
					txRecordList.Add((dateTime, coin.Height, coin.Amount, coin.Label, coin.TransactionId));
				}

				if (coin.SpenderTransactionId != null)
				{
					SmartTransaction foundSpenderTransaction = Global.WalletService.TransactionCache.First(x => x.GetHash() == coin.SpenderTransactionId);
					if (foundSpenderTransaction.Height.Type == HeightType.Chain)
					{
						if (Global.WalletService?.ProcessedBlocks != null) // NullReferenceException appeared here.
						{
							if (Global.WalletService.ProcessedBlocks.Any(x => x.Value.height == foundSpenderTransaction.Height))
							{
								if (Global.WalletService?.ProcessedBlocks != null) // NullReferenceException appeared here.
								{
									dateTime = Global.WalletService.ProcessedBlocks.First(x => x.Value.height == foundSpenderTransaction.Height).Value.dateTime;
								}
								else
								{
									dateTime = DateTimeOffset.UtcNow;
								}
							}
							else
							{
								dateTime = DateTimeOffset.UtcNow;
							}
						}
						else
						{
							dateTime = DateTimeOffset.UtcNow;
						}
					}
					else
					{
						dateTime = foundSpenderTransaction.FirstSeenIfMemPoolTime ?? DateTimeOffset.UtcNow;
					}

					var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.transactionId == coin.SpenderTransactionId);
					if (foundSpenderCoin != default) // if found
					{
						txRecordList.Remove(foundSpenderCoin);
						var newRecord = (dateTime, foundSpenderTransaction.Height, foundSpenderCoin.amount - coin.Amount, foundSpenderCoin.label, coin.SpenderTransactionId);
						txRecordList.Add(newRecord);
					}
					else
					{
						txRecordList.Add((dateTime, foundSpenderTransaction.Height, (Money.Zero - coin.Amount), "", coin.SpenderTransactionId));
					}
				}
			}
			txRecordList = txRecordList.OrderByDescending(x => x.dateTime).ThenBy(x => x.amount).ToList();
			return txRecordList;
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

		public bool IsCoinJoinTransactionVisible => Global.UiConfig.CoinJoinTransactionVisible == true;

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
