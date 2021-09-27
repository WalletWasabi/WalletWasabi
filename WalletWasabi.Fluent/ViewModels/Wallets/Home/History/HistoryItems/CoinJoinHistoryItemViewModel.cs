using System;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;

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
