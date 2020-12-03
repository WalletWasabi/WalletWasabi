using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	[NavigationMetaData(
		Title = "Broadcaster",
		Caption = "Broadcast your transactions here",
		IconName = "live_regular",
		Order = 5,
		Category = "General",
		Keywords = new[] { "Transaction Id", "Input", "Output", "Amount", "Network", "Fee", "Count", "BTC", "Signed", "Paste", "Import", "Broadcast", "Transaction", },
		NavBarPosition = NavBarPosition.None)]
	public partial class BroadcastTransactionViewModel : RoutableViewModel
	{
		[AutoNotify] private string? _transactionId;
		[AutoNotify] private Money? _totalInputValue;
		[AutoNotify] private Money? _totalOutputValue;
		[AutoNotify] private int _inputCount;
		[AutoNotify] private int _outputCount;
		[AutoNotify] private SmartTransaction? _transaction;
		private readonly Network _network;
		private readonly BitcoinStore _store;

		public BroadcastTransactionViewModel(BitcoinStore store, Network network, TransactionBroadcaster broadcaster)
		{
			_network = network;
			_store = store;

			var nextCommandCanExecute = this.WhenAnyValue(
					x => x.IsBusy,
					x => x.Transaction)
				.Select(x => !x.Item1 && x.Item2 is { });

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					if (Transaction is { })
					{
						IsBusy = true;

						await broadcaster.SendTransactionAsync(Transaction);

						Navigate().Back();

						IsBusy = false;
					}
				},
				nextCommandCanExecute);
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			if (!inStack)
			{
				IsBusy = true;

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						var result = await NavigateDialog(new LoadTransactionViewModel(_network));

						if (result is { })
						{
							var nullMoney = new Money(-1L);
							var nullOutput = new TxOut(nullMoney, Script.Empty);

							var psbt = PSBT.FromTransaction(result.Transaction, _network);

							TxOut GetOutput(OutPoint outpoint) =>
								_store.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
									? prevTxn.Transaction.Outputs[outpoint.N]
									: nullOutput;

							var inputAddressAmount = psbt.Inputs
								.Select(x => x.PrevOut)
								.Select(GetOutput)
								.ToArray();

							var outputAddressAmount = psbt.Outputs
								.Select(x => x.GetCoin().TxOut)
								.ToArray();

							var psbtTxn = psbt.GetOriginalTransaction();

							TransactionId = psbtTxn.GetHash().ToString();
							InputCount = inputAddressAmount.Length;
							OutputCount = outputAddressAmount.Length;
							TotalInputValue = inputAddressAmount.Any(x => x.Value == nullMoney)
								? null
								: inputAddressAmount.Select(x => x.Value).Sum();
							TotalOutputValue = outputAddressAmount.Any(x => x.Value == nullMoney)
								? null
								: outputAddressAmount.Select(x => x.Value).Sum();
							Transaction = result;
						}
						else
						{
							Navigate().Back();
						}

						IsBusy = false;
					});
			}
		}

		public Money NetworkFee => TotalInputValue - TotalOutputValue;
	}
}
