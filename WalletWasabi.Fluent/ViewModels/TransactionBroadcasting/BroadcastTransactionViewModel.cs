using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Stores;

namespace WalletWasabi.Fluent.ViewModels.TransactionBroadcasting
{
	public partial class BroadcastTransactionViewModel : RoutableViewModel
	{
		[AutoNotify] private string _transactionId;
		[AutoNotify] private Money? _totalInputValue;
		[AutoNotify] private Money? _totalOutputValue;
		[AutoNotify] private int _inputCount;
		[AutoNotify] private int _outputCount;

		public BroadcastTransactionViewModel(Global global, SmartTransaction finalTransaction)
		{
			var psbt = PSBT.FromTransaction(finalTransaction.Transaction, global.Network);
			var nullMoney = new Money(-1L);
			var nullOutput = new TxOut(nullMoney, Script.Empty);

			TxOut GetOutput(OutPoint outpoint) =>
				global.BitcoinStore.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
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
			_totalInputValue = inputAddressAmount.Any(x => x.Value == nullMoney) ? null : inputAddressAmount.Select(x => x.Value).Sum();
			_totalOutputValue = outputAddressAmount.Any(x => x.Value == nullMoney) ? null : outputAddressAmount.Select(x => x.Value).Sum();

			var nextCommandCanExecute = this.WhenAnyValue(x => x.IsBusy).Select(x => !x);
			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					IsBusy = true;

					// Wait until broadcaster is not available
					while (global.TransactionBroadcaster is null)
					{
						await Task.Delay(100);
					}

					await global.TransactionBroadcaster.SendTransactionAsync(finalTransaction);
					Navigate().Clear();
					IsBusy = false;
				}
				, nextCommandCanExecute);
		}

		public Money NetworkFee => TotalInputValue - TotalOutputValue;
	}
}
