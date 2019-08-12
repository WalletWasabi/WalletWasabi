using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfo : ReactiveObject
	{
		private int _confirmations;
		private bool _confirmed;
		private DateTimeOffset _dateTime;
		private Money _amount;
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

		public bool Confirmed
		{
			get => _confirmed;
			set => this.RaiseAndSetIfChanged(ref _confirmed, value);
		}

		public Money Amount
		{
			get => _amount;
			set => this.RaiseAndSetIfChanged(ref _amount, value);
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
