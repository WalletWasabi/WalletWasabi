using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems
{
	public class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
	{
		private List<TransactionSummary> _coinJoinTransactions;

		public CoinJoinsHistoryItemViewModel(int orderIndex, TransactionSummary firstItem)
			: base(orderIndex, firstItem)
		{
			_coinJoinTransactions = new List<TransactionSummary>( );
			Label = new List<string> { "Privacy Increasement" };
			FilteredLabel = new List<string>();
			IsCoinJoin = true;

			ShowDetailsCommand = ReactiveCommand.Create(() =>
				RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(new CoinJoinDetailsViewModel()));

			Add(firstItem);
		}

		public void Add(TransactionSummary item)
		{
			if (!item.IsLikelyCoinJoinOutput)
			{
				throw new InvalidOperationException("Not a coinjoin item!");
			}

			_coinJoinTransactions.Add(item);
			Refresh();
		}

		private void Refresh()
		{
			IsConfirmed = _coinJoinTransactions.All(x => x.IsConfirmed());
			Date = _coinJoinTransactions.Last().DateTime.ToLocalTime();
			UpdateAmount();
			UpdateDateString();
		}

		public override void Update(HistoryItemViewModelBase item)
		{
			if (item is not CoinJoinsHistoryItemViewModel coinJoinHistoryItemViewModel)
			{
				throw new InvalidOperationException("Not the same type!");
			}

			_coinJoinTransactions = coinJoinHistoryItemViewModel._coinJoinTransactions;
			UpdateAmount();

			base.Update(item);
		}

		protected void UpdateDateString()
		{
			var firstDate = _coinJoinTransactions.First().DateTime.ToLocalTime();
			var lastDate = _coinJoinTransactions.Last().DateTime.ToLocalTime();

			DateString = firstDate.Day == lastDate.Day
				? $"{firstDate:MM/dd/yyyy}"
				: $"{firstDate:MM/dd/yy} - {lastDate:MM/dd/yy}";
		}

		private void UpdateAmount()
		{
			OutgoingAmount = _coinJoinTransactions.Sum(x => x.Amount) * -1;
		}

		public void SetBalance(Money balance)
		{
			Balance = balance;
		}
	}
}
