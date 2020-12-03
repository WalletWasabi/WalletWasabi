using System.Linq;
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
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class BroadcastTransactionViewModel : RoutableViewModel
	{
		[AutoNotify] private string? _transactionId;
		[AutoNotify] private Money? _totalInputValue;
		[AutoNotify] private Money? _totalOutputValue;
		[AutoNotify] private int _inputCount;
		[AutoNotify] private int _outputCount;
		[AutoNotify] private Money _networkFee;
		public BroadcastTransactionViewModel(
			BitcoinStore store,
			Network network,
			TransactionBroadcaster broadcaster,
			SmartTransaction transaction)
		{
			var nullMoney = new Money(-1L);
			var nullOutput = new TxOut(nullMoney, Script.Empty);

			var psbt = PSBT.FromTransaction(transaction.Transaction, network);

			TxOut GetOutput(OutPoint outpoint) =>
				store.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
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
			_networkFee = TotalInputValue is null || TotalOutputValue is null
				? null
				: TotalInputValue - TotalOutputValue;

			var nextCommandCanExecute = this.WhenAnyValue(x => x.IsBusy)
				.Select(x => !x);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					IsBusy = true;

					await broadcaster.SendTransactionAsync(transaction);

					Navigate().Back();

					IsBusy = false;
				},
				nextCommandCanExecute);
		}
	}
}
