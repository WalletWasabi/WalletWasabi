using System;
using System.Linq;

using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Gui.Controls.TransactionDetails.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Gui.Controls.TransactionDetails.ViewModels
{
	public class TransactionDetailsViewModel : ReactiveObject
	{
		private int _confirmations;
		private bool _confirmed;
		private DateTimeOffset _dateTime;
		private string _amountBtc;
		private string _label;
		private int _blockHeight;
		private string _transactionId;
		private string _totalInputValue;
		private string _totalOutputValue;
		private int _inputCount;
		private int _outputCount;

		public DateTimeOffset DateTime
		{
			get => _dateTime;
			set => this.RaiseAndSetIfChanged(ref _dateTime, value);
		}

		public int Confirmations
		{
			get => _confirmations;
			set => this.RaiseAndSetIfChanged(ref _confirmations, value);
		}

		public bool Confirmed
		{
			get => _confirmed;
			set => this.RaiseAndSetIfChanged(ref _confirmed, value);
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

		public string TotalInputValue
		{
			get => _totalInputValue;
			set => this.RaiseAndSetIfChanged(ref _totalInputValue, value);
		}

		public string TotalOutputValue
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

		public static TransactionDetailsViewModel FromBuildTxnResult(BitcoinStore stores, BuildTransactionResult result)
		{
			AddressAmountTuple FromTxOutput(TxOut output) =>
				new AddressAmountTuple(output?.ScriptPubKey.GetDestinationAddress(result.Network).ToString() ?? string.Empty, output?.Value ?? Money.Zero);

			TxOut GetOutput(OutPoint outpoint) =>
				stores.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
					? prevTxn.Transaction.Outputs[outpoint.N]
					: null;

			var inputAddrMoney = result.Transaction.Transaction.Inputs
				.Select(x => x.PrevOut)
				.Select(GetOutput)
				.Select(FromTxOutput);

			var outputAddrMoney = result.Transaction.Transaction.Outputs.Select(FromTxOutput);

			var totalInValue = inputAddrMoney.Select(x => x.Amount).Sum().ToString();
			var totalOutValue = outputAddrMoney.Select(x => x.Amount).Sum().ToString();

			return new TransactionDetailsViewModel()
			{
				TransactionId = result.Transaction.GetHash().ToString(),
				Confirmed = false,
				DateTime = result.Transaction.FirstSeen,
				InputCount = inputAddrMoney.Count(),
				OutputCount = outputAddrMoney.Count(),
				TotalInputValue = totalInValue,
				TotalOutputValue = totalOutValue,
			};
		}
	}
}
