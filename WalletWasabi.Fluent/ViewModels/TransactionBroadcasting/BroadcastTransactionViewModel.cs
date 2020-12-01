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
	public class BroadcastTransactionViewModel : RoutableViewModel
	{
		private string _transactionId;
		private Money? _totalInputValue;
		private Money? _totalOutputValue;
		private int _inputCount;
		private int _outputCount;

		public BroadcastTransactionViewModel(
			BitcoinStore store,
			SmartTransaction finalTransaction,
			Network network,
			TransactionBroadcaster? transactionBroadcaster)
		{
			var psbt = PSBT.FromTransaction(finalTransaction.Transaction, network);
			var nullMoney = new Money(-1L);
			var nullOutput = new TxOut(nullMoney, Script.Empty);

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

			_transactionId = psbtTxn.GetHash().ToString();
			_inputCount = inputAddressAmount.Length;
			_outputCount = outputAddressAmount.Length;
			_totalInputValue = inputAddressAmount.Any(x => x.Value == nullMoney) ? null : inputAddressAmount.Select(x => x.Value).Sum();
			_totalOutputValue = outputAddressAmount.Any(x => x.Value == nullMoney) ? null : outputAddressAmount.Select(x => x.Value).Sum();

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				// Transaction broadcaster is not ready while backend is not connected.
				if (transactionBroadcaster is null)
				{
					return;
				}

				IsBusy = true;
				await transactionBroadcaster.SendTransactionAsync(finalTransaction);
				Navigate().Clear();
				IsBusy = false;
			});
		}

		public string TransactionId
		{
			get => _transactionId;
			set => this.RaiseAndSetIfChanged(ref _transactionId, value);
		}

		public Money? TotalInputValue
		{
			get => _totalInputValue;
			set => this.RaiseAndSetIfChanged(ref _totalInputValue, value);
		}

		public Money? TotalOutputValue
		{
			get => _totalOutputValue;
			set => this.RaiseAndSetIfChanged(ref _totalOutputValue, value);
		}

		public int InputCount
		{
			get => _inputCount;
			set => this.RaiseAndSetIfChanged(ref _inputCount, value);
		}

		public int OutputCount
		{
			get => _outputCount;
			set => this.RaiseAndSetIfChanged(ref _outputCount, value);
		}

		public Money NetworkFee => TotalInputValue - TotalOutputValue;
	}
}
