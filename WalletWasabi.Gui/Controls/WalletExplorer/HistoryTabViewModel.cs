using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<TransactionViewModel> _transactions;
		private TransactionViewModel _selectedTransaction;
		private SortOrder _dateSortDirection;
		private SortOrder _amountSortDirection;
		private SortOrder _transactionSortDirection;
		private bool _isFirstLoading;

		public bool IsFirstLoading
		{
			get => _isFirstLoading;
			set => this.RaiseAndSetIfChanged(ref _isFirstLoading, value);
		}

		public ReactiveCommand<Unit, Unit> SortCommand { get; }

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			IsFirstLoading = true;

			Transactions = new ObservableCollection<TransactionViewModel>();

			this.WhenAnyValue(x => x.SelectedTransaction).Subscribe(async transaction =>
				{
					if (Global.UiConfig?.Autocopy is false || transaction is null)
					{
						return;
					}

					await transaction.TryCopyTxIdToClipboardAsync();
				});

			SortCommand = ReactiveCommand.Create(RefreshOrdering);

			DateSortDirection = SortOrder.Decreasing;

			_ = TryRewriteTableAsync();
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.Merge(Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.NewBlockProcessed)))
				.Merge(Observable.FromEventPattern(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.CoinSpent)))
				.Merge(Observable.FromEventPattern(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.SpenderConfirmed)))
				.Throttle(TimeSpan.FromSeconds(5))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await TryRewriteTableAsync())
				.DisposeWith(Disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
				{
					foreach (var transaction in Transactions)
					{
						transaction.Refresh();
					}
				}).DisposeWith(Disposables);
		}

		public override bool OnClose()
		{
			Disposables.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		private async Task TryRewriteTableAsync()
		{
			try
			{
				var txRecordList = await Task.Run(BuildTxRecordList);

				var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
				Transactions?.Clear();

				var trs = txRecordList.Select(txr => new TransactionInfo
				{
					DateTime = txr.DateTime.ToLocalTime(),
					Confirmed = txr.Height.Type == HeightType.Chain,
					Confirmations = txr.Height.Type == HeightType.Chain ? Global.BitcoinStore.HashChain.TipHeight - txr.Height.Value + 1 : 0,
					AmountBtc = $"{txr.Amount.ToString(fplus: true, trimExcessZero: true)}",
					Label = txr.Label,
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
			finally
			{
				IsFirstLoading = false;
			}
		}

		class TransactionData
		{
			public DateTimeOffset DateTime { get; set; }
			public Height Height { get; set; }
			public Money Amount { get; set; }
			public string Label { get; set; }
			public uint256 TransactionId { get; set; }
		}

		private List<TransactionData> BuildTxRecordList()
		{
			var walletService = Global.WalletService;

			var txRecordList = new List<TransactionData>();
			if (walletService is null)
			{
				return txRecordList;
			}

			var processedBlockTimeByHeigh = walletService.ProcessedBlocks?.Values.ToDictionary(x=>x.height, x=>x.dateTime)
				?? new Dictionary<Height, DateTimeOffset>();
			foreach (SmartCoin coin in walletService.Coins)
			{
				var foundTransaction = walletService.TryGetTxFromCache(coin.TransactionId);
				if (foundTransaction is null)
				{
					continue;
				}

				DateTimeOffset dateTime;
				if (foundTransaction.Height.Type == HeightType.Chain)
				{
					if(processedBlockTimeByHeigh.TryGetValue(foundTransaction.Height, out var blockDateTime))
					{
						dateTime = blockDateTime;
					}
					else
					{
						dateTime = DateTimeOffset.UtcNow;
					}
				}
				else
				{
					dateTime = foundTransaction.FirstSeen;
				}

				var found = txRecordList.FirstOrDefault(x => x.TransactionId == coin.TransactionId);
				if (found != null) // if found
				{
					var label = found.Label != string.Empty ? found.Label + ", " : "";
					found.DateTime = dateTime; 
					found.Amount += coin.Amount;
					found.Label = $"{label}{coin.Label}";
				}
				else
				{
					txRecordList.Add(new TransactionData{
						DateTime = dateTime,
						Height = coin.Height, 
						Amount = coin.Amount, 
						Label = coin.Label.ToString(),
						TransactionId = coin.TransactionId});
				}

				if (!coin.Unspent)
				{
					var foundSpenderTransaction = walletService.TryGetTxFromCache(coin.SpenderTransactionId);
					if (foundSpenderTransaction is null)
					{
						throw new InvalidOperationException($"Transaction {coin.SpenderTransactionId} not found.");
					}

					if (foundSpenderTransaction.Height.Type == HeightType.Chain)
					{
						if(processedBlockTimeByHeigh.TryGetValue(foundSpenderTransaction.Height, out var blockDateTime))
						{
							dateTime = blockDateTime;
						}
						else
						{
							dateTime = DateTimeOffset.UtcNow;
						}
					}
					else
					{
						dateTime = foundSpenderTransaction.FirstSeen;
					}

					var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.TransactionId == coin.SpenderTransactionId);
					if (foundSpenderCoin != null) // if found
					{
						foundSpenderCoin.DateTime = dateTime; 
						foundSpenderCoin.Amount -= coin.Amount;
					}
					else
					{
						txRecordList.Add(new TransactionData{
							DateTime = dateTime,
							Height = foundSpenderTransaction.Height, 
							Amount = (Money.Zero - coin.Amount), 
							Label = "",
							TransactionId = coin.SpenderTransactionId});

					}
				}
			}
			txRecordList = txRecordList.OrderByDescending(x => x.DateTime).ThenBy(x => x.Amount).ToList();
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
