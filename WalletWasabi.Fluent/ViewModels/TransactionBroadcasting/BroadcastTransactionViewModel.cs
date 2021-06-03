using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

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
		[AutoNotify] private string _transactionId;
		[AutoNotify] private Money? _totalInputValue;
		[AutoNotify] private Money? _totalOutputValue;
		[AutoNotify] private int _inputCount;
		[AutoNotify] private int _outputCount;
		[AutoNotify] private Money? _networkFee;

		public BroadcastTransactionViewModel(Network network, SmartTransaction transaction)
		{
			Title = "Broadcast Transaction";

			var nullMoney = new Money(-1L);
			var nullOutput = new TxOut(nullMoney, Script.Empty);

			var psbt = PSBT.FromTransaction(transaction.Transaction, network);

			TxOut GetOutput(OutPoint outpoint) =>
				Services.BitcoinStore.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
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

			_transactionId = psbtTxn.GetHash().ToString();
			_inputCount = inputAddressAmount.Length;
			_outputCount = outputAddressAmount.Length;
			_totalInputValue = inputAddressAmount.Any(x => x.Value == nullMoney)
				? null
				: inputAddressAmount.Select(x => x.Value).Sum();
			_totalOutputValue = outputAddressAmount.Any(x => x.Value == nullMoney)
				? null
				: outputAddressAmount.Select(x => x.Value).Sum();
			_networkFee = TotalInputValue is null || TotalOutputValue is null
				? null
				: TotalInputValue - TotalOutputValue;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			EnableBack = false;

			this.WhenAnyValue(x => x.IsBusy)
				.Subscribe(x => EnableCancel = !x);

			var nextCommandCanExecute = this.WhenAnyValue(x => x.IsBusy)
				.Select(x => !x);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNextAsync(transaction),
				nextCommandCanExecute);

			EnableAutoBusyOn(NextCommand);
		}

		private async Task OnNextAsync(SmartTransaction transaction)
		{
			try
			{
				await Services.TransactionBroadcaster.SendTransactionAsync(transaction);
				Navigate().To(new SuccessBroadcastTransactionViewModel());
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "It was not possible to broadcast the transaction.");
			}
		}
	}
}
