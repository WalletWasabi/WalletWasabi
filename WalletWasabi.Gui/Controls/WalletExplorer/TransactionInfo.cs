using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfo : ReactiveObject
	{
		private int _confirmations;
		private int _blockHeight;
		private int _anonymitySet;
		private bool _confirmed;
		private DateTimeOffset _dateTime;
		private string _amountBtc;
		private string _label;
		private string _transactionId;
		private string _address;
		private string _scriptPubKeyHex;
		private string _spendingTx;

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

		public int BlockHeight
		{
			get => _blockHeight;
			set => this.RaiseAndSetIfChanged(ref _blockHeight, value);
		}

		public int AnonymitySet
		{
			get => _anonymitySet;
			set => this.RaiseAndSetIfChanged(ref _anonymitySet, value);
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

		public string TransactionId
		{
			get => _transactionId;
			set => this.RaiseAndSetIfChanged(ref _transactionId, value);
		}

		public string Address
		{
			get => _address;
			set => this.RaiseAndSetIfChanged(ref _address, value);
		}

		public string ScriptPubKeyHex
		{
			get => _scriptPubKeyHex;
			set => this.RaiseAndSetIfChanged(ref _scriptPubKeyHex, value);
		}

		public string SpendingTx
		{
			get => _spendingTx;
			set => this.RaiseAndSetIfChanged(ref _spendingTx, value);
		}
	}
}
