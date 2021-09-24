using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems
{
	public abstract partial class HistoryItemViewModelBase : ViewModelBase
	{
		[AutoNotify] private bool _isFlashing;
		[AutoNotify] private int _orderIndex;
		[AutoNotify] private DateTimeOffset _date;
		[AutoNotify] private string? _dateString;
		[AutoNotify] private bool _isConfirmed;

		protected HistoryItemViewModelBase(int orderIndex, TransactionSummary transactionSummary, Money balance)
		{
			TransactionSummary = transactionSummary;
			Date = transactionSummary.DateTime.ToLocalTime();
			OrderIndex = orderIndex;
			Balance = balance;
			IsConfirmed = transactionSummary.IsConfirmed();
			Label = transactionSummary.Label.Take(1).ToList();
			FilteredLabel = transactionSummary.Label.Skip(1).ToList();

			this.WhenAnyValue(x => x.IsFlashing)
				.Where(x => x)
				.Subscribe(async _ =>
				{
					await Task.Delay(1260);
					IsFlashing = false;
				});
		}

		public uint256 TransactionId => TransactionSummary.TransactionId;

		public List<string> FilteredLabel { get; protected set; }

		public List<string> Label { get; protected set; }

		public bool IsCoinJoin { get; protected set; }

		public TransactionSummary TransactionSummary { get; }

		public Money Balance { get; set; }

		public Money? OutgoingAmount { get; protected set; }

		public Money? IncomingAmount { get; protected set; }

		public ICommand? ShowDetailsCommand { get; protected set; }

		public virtual void Update(HistoryItemViewModelBase item)
		{
			OrderIndex = item.OrderIndex;
			Date = item.Date;
			IsConfirmed = item.IsConfirmed;
		}

		protected abstract void UpdateDateString();
	}
}
