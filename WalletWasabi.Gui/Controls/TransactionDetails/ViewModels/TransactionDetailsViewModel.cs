using System;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Stores;

namespace WalletWasabi.Gui.Controls.TransactionDetails.ViewModels
{
	public class TransactionDetailsViewModel : ViewModelBase
	{
		private int _confirmations;
		private DateTimeOffset _dateTime;
		private string _amountBtc;
		private string _label;
		private int _blockHeight;
		private string _transactionId;
		private Money _totalInputValue;
		private Money _totalOutputValue;
		private int _inputCount;
		private int _outputCount;

		public DateTimeOffset DateTime
		{
			get => _dateTime;
			set => this.RaiseAndSetIfChanged(ref _dateTime, value);
		}

		public bool Confirmed => Confirmations > 0;

		public int Confirmations
		{
			get => _confirmations;
			set => this.RaiseAndSetIfChanged(ref _confirmations, value);
		}

		public string AmountBtc
		{
			get => _amountBtc;
			set => this.RaiseAndSetIfChanged(ref _amountBtc, value);
		}

		public string Label
		{
			get => _label;
			set => this.RaiseAndSetIfChanged(ref _label, value);
		}

		public int BlockHeight
		{
			get => _blockHeight;
			set => this.RaiseAndSetIfChanged(ref _blockHeight, value);
		}

		public string TransactionId
		{
			get => _transactionId;
			set => this.RaiseAndSetIfChanged(ref _transactionId, value);
		}

		public Money TotalInputValue
		{
			get => _totalInputValue;
			set => this.RaiseAndSetIfChanged(ref _totalInputValue, value);
		}

		public Money TotalOutputValue
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

		public Money NetworkFee => TotalInputValue is null || TotalOutputValue is null
			? null
			: TotalInputValue - TotalOutputValue;

		public static TransactionDetailsViewModel FromBuildTxnResult(BitcoinStore store, PSBT psbt)
		{
			var nullMoney = new Money(-1L);
			var nullOutput = new TxOut(nullMoney, Script.Empty);

			TxOut GetOutput(OutPoint outpoint) =>
				store.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
					? prevTxn.Transaction.Outputs[outpoint.N]
					: nullOutput;

			var inputAddressAmount = psbt.Inputs
				.Select(x => x.PrevOut)
				.Select(GetOutput);

			var outputAddressAmount = psbt.Outputs
				.Select(x => x.GetCoin().TxOut);

			var psbtTxn = psbt.GetOriginalTransaction();

			return new TransactionDetailsViewModel()
			{
				TransactionId = psbtTxn.GetHash().ToString(),
				InputCount = inputAddressAmount.Count(),
				OutputCount = outputAddressAmount.Count(),
				TotalInputValue = inputAddressAmount.Any(x => x.Value == nullMoney) ? null : inputAddressAmount.Select(x => x.Value).Sum(),
				TotalOutputValue = outputAddressAmount.Any(x => x.Value == nullMoney) ? null : outputAddressAmount.Select(x => x.Value).Sum(),
			};
		}
	}
}
