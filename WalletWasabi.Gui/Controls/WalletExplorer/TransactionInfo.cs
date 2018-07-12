using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfo : ReactiveObject
	{
		private bool _confirmed;
		private DateTimeOffset _dateTime;
		private string _amountBtc;
		private string _label;
		private string _transactionId;

		public DateTimeOffset DateTime
		{
			get { return _dateTime; }
			set { this.RaiseAndSetIfChanged(ref _dateTime, value); }
		}

		public bool Confirmed
		{
			get { return _confirmed; }
			set { this.RaiseAndSetIfChanged(ref _confirmed, value); }
		}

		public string AmountBtc
		{
			get { return _amountBtc; }
			set { this.RaiseAndSetIfChanged(ref _amountBtc, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public string TransactionId
		{
			get { return _transactionId; }
			set { this.RaiseAndSetIfChanged(ref _transactionId, value); }
		}
	}
}
