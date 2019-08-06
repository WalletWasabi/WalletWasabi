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
		private int _size;
		private int _virtualSize;
		private bool _confirmed;
		private bool _rbf;
		private DateTimeOffset _dateTime;
		private string _fees;
		private string _amountBtc;
		private string _label;
		private string _transactionId;

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

		public int Size
		{
			get => _size;
			set => this.RaiseAndSetIfChanged(ref _size, value);
		}

		public int VirtualSize
		{
			get => _virtualSize;
			set => this.RaiseAndSetIfChanged(ref _virtualSize, value);
		}

		public bool Confirmed
		{
			get => _confirmed;
			set => this.RaiseAndSetIfChanged(ref _confirmed, value);
		}

		public bool RBF
		{
			get => _rbf;
			set => this.RaiseAndSetIfChanged(ref _rbf, value);
		}

		public string Fees
		{
			get => _fees;
			set => this.RaiseAndSetIfChanged(ref _fees, value);
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
	}
}
