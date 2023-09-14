using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
{
	private readonly WalletViewModel _walletVm;

	private CoinJoinsHistoryItemViewModel(
		int orderIndex,
		SmartTransaction firstItem,
		WalletViewModel walletVm)
		: base(orderIndex, firstItem)
	{
		_walletVm = walletVm;

		CoinJoinTransactions = new List<SmartTransaction>();
		IsCoinJoin = true;
		IsCoinJoinGroup = true;

		ShowDetailsCommand = ReactiveCommand.Create(
			() =>
			UiContext.Navigate(NavigationTarget.DialogScreen).To(
				new CoinJoinsDetailsViewModel(this, walletVm.UiTriggers.TransactionsUpdateTrigger)));

		Add(firstItem);
	}

	public List<SmartTransaction> CoinJoinTransactions { get; private set; }

	protected override ObservableCollection<HistoryItemViewModelBase> LoadChildren()
	{
		var result = new ObservableCollection<HistoryItemViewModelBase>();

		var balance = Balance ?? Money.Zero;

		for (var i = 0; i < CoinJoinTransactions.Count; i++)
		{
			var item = CoinJoinTransactions[i];

			var transaction = new CoinJoinHistoryItemViewModel(
				UiContext,
				i,
				item,
				_walletVm,
				balance,
				false);

			balance -= item.GetAmount();

			result.Add(transaction);
		}

		return result;
	}

	public override bool HasChildren()
	{
		if (CoinJoinTransactions.Count > 1)
		{
			return true;
		}

		return false;
	}

	public void Add(SmartTransaction item)
	{
		if (!item.IsOwnCoinjoin())
		{
			throw new InvalidOperationException("Not a coinjoin item!");
		}

		CoinJoinTransactions.Insert(0, item);
		Refresh();
	}

	private void Refresh()
	{
		IsConfirmed = CoinJoinTransactions.All(x => x.Confirmed);
		ConfirmedToolTip = GetConfirmedToolTip(CoinJoinTransactions.Select(x => x.GetConfirmations((int)Services.BitcoinStore.SmartHeaderChain.ServerTipHeight)).Min());
		Date = CoinJoinTransactions.Select(tx => tx.FirstSeen).Max().ToLocalTime();

		SetAmount(
			CoinJoinTransactions.Sum(x => x.GetAmount()),
			CoinJoinTransactions.Sum(x => x.GetFee() ?? Money.Zero));

		UpdateDateString();
	}

	protected void UpdateDateString()
	{
		var dates = CoinJoinTransactions.Select(tx => tx.FirstSeen).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();

		DateString = firstDate.Day == lastDate.Day
			? $"{firstDate.ToUserFacingString(withTime: false)}"
			: $"{firstDate.ToUserFacingString(withTime: false)} - {lastDate.ToUserFacingString(withTime: false)}";
	}

	public void SetBalance(Money balance)
	{
		Balance = balance;
	}
}
