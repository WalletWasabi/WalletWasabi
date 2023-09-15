using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
{
	private CoinJoinHistoryItemViewModel(
		int orderIndex,
		SmartTransaction transaction,
		Money amount,
		WalletViewModel walletVm,
		Money balance,
		bool isSingleCoinJoinTransaction)
		: base(orderIndex, transaction)
	{
		Date = transaction.FirstSeen.ToLocalTime();
		Balance = balance;
		IsCoinJoin = true;
		CoinJoinTransaction = transaction;
		IsChild = !isSingleCoinJoinTransaction;

		SetAmount(amount, transaction.GetFee());

		ShowDetailsCommand = ReactiveCommand.Create(() =>
			UiContext.Navigate(NavigationTarget.DialogScreen).To(
				new CoinJoinDetailsViewModel(this, walletVm.UiTriggers.TransactionsUpdateTrigger)));

		DateString = Date.ToLocalTime().ToUserFacingString();
	}

	public SmartTransaction CoinJoinTransaction { get; private set; }
}
