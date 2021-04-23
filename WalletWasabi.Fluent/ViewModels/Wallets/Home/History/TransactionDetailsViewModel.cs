using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
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
		private readonly IObservable<Unit> _updateTrigger;

		[AutoNotify] private bool _isConfirmed;
		[AutoNotify] private int _confirmations;
		[AutoNotify] private int _blockHeight;
		[AutoNotify] private DateTimeOffset _date;
		[AutoNotify] private string? _amount;
		[AutoNotify] private SmartLabel? _labels;
		[AutoNotify] private uint256? _transactionId;

		public TransactionDetailsViewModel(TransactionSummary transactionSummary, BitcoinStore bitcoinStore, Wallet wallet, IObservable<Unit> updateTrigger)
		{
			_bitcoinStore = bitcoinStore;
			_wallet = wallet;
			_updateTrigger = updateTrigger;

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
			Confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int)_bitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = Confirmations > 0;
			Amount = transactionSummary.Amount.ToString(fplus: false);
		}

		private void OnNext()
		{
			Navigate().Clear();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			_updateTrigger
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
