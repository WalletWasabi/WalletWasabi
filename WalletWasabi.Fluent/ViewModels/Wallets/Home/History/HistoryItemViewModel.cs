using System;
using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public class HistoryItemViewModel
	{
		public HistoryItemViewModel(int orderIndex, TransactionSummary transactionSummary, Wallet wallet, Money balance, IObservable<Unit> updateTrigger)
		{
			TransactionSummary = transactionSummary;
			Date = transactionSummary.DateTime.ToLocalTime();
			IsCoinJoin = transactionSummary.IsLikelyCoinJoinOutput;
			OrderIndex = orderIndex;
			Balance = balance;

			var confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int) wallet.BitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
			IsConfirmed = confirmations > 0;

			var amount = transactionSummary.Amount;
			if (amount < Money.Zero)
			{
				OutgoingAmount = amount * -1;
			}
			else
			{
				IncomingAmount = amount;
			}

			ShowDetailsCommand = ReactiveCommand.Create(() =>
			{
				if (NavigationState.Instance.DialogScreenNavigation is { } navStack)
				{
					navStack.To(new TransactionDetailsViewModel(transactionSummary, wallet, updateTrigger));
				}
			});
		}

		public ICommand ShowDetailsCommand { get; }

		public TransactionSummary TransactionSummary { get; }

		public int OrderIndex { get; }

		public Money Balance { get; set; }

		public DateTimeOffset Date { get; set; }

		public bool IsConfirmed { get; }

		public Money? IncomingAmount { get; }

		public Money? OutgoingAmount { get; }

		public bool IsCoinJoin { get; }
	}
}
