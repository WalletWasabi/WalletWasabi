using System.Transactions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.TransactionBuilding;
using NBitcoin;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfo : ReactiveObject
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

		public static TransactionInfo FromBuildTxnResult(BuildTransactionResult result)
		{
			AddressMoneyTuple FromTxOutput(TxOut output) =>
				new AddressMoneyTuple(output.ScriptPubKey.GetDestinationAddress(result.Network).ToString(), output.Value);

			TxOut GetOutput(OutPoint outpoint) =>
				result.Store.TransactionStore.TryGetTransaction(outpoint.Hash, out var prevTxn)
					? prevTxn.Transaction.Outputs[outpoint.N]
					: null;

			var inputAddrMoney = result.Transaction.Transaction.Inputs
				.Select(x => x.PrevOut)
				.Select(GetOutput)
				.Where(x => x is { })
				.Select(FromTxOutput);

			var outputAddrMoney = result.Transaction.Transaction.Outputs.Select(FromTxOutput);


			var totalInValue = inputAddrMoney.Select(x => x.Amount).Sum().ToString();
			var totalOutValue = outputAddrMoney.Select(x => x.Amount).Sum().ToString();

			return new TransactionInfo()
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
