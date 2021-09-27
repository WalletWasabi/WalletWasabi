using System;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems
{
	public class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
	{
		public CoinJoinHistoryItemViewModel(
			int orderIndex,
			TransactionSummary transactionSummary,
			WalletViewModel walletViewModel,
			Money balance,
			DateTimeOffset lastCjDateInGroup)
			: base(orderIndex, transactionSummary, balance)
		{
			LastCjDateInGroup = lastCjDateInGroup;
			OutgoingAmount = transactionSummary.Amount * -1;
			IsCoinJoin = true;

			ShowDetailsCommand = ReactiveCommand.Create(() =>
				RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(new CoinJoinDetailsViewModel()));

			UpdateDateString();
		}

		public DateTimeOffset LastCjDateInGroup { get; private set; }

		public override void Update(HistoryItemViewModelBase item)
		{
			if (item is not CoinJoinHistoryItemViewModel coinJoinHistoryItemViewModel)
			{
				throw new InvalidOperationException("Not the same type!");
			}

			LastCjDateInGroup = coinJoinHistoryItemViewModel.LastCjDateInGroup;

			base.Update(item);
		}

		protected sealed override void UpdateDateString()
		{
			DateString = Date.Day == LastCjDateInGroup.Day
				? $"{Date.ToLocalTime():MM/dd/yyyy}"
				: $"{Date.ToLocalTime():MM/dd/yy} - {LastCjDateInGroup.ToLocalTime():MM/dd/yy}";
		}
	}
}
