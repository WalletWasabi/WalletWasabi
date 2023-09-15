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
		Money firstAmount,
		WalletViewModel walletVm)
		: base(orderIndex, firstItem)
	{
		_walletVm = walletVm;

		CoinJoinTransactions = new List<SmartTransaction>();
		Amounts = new Dictionary<uint256, Money>();
		IsCoinJoin = true;
		IsCoinJoinGroup = true;

		ShowDetailsCommand = ReactiveCommand.Create(
			() =>
			UiContext.Navigate(NavigationTarget.DialogScreen).To(
				new CoinJoinsDetailsViewModel(this, walletVm.UiTriggers.TransactionsUpdateTrigger)));

		Add(firstItem, firstAmount);
	}

	public List<SmartTransaction> CoinJoinTransactions { get; private set; }
	private Dictionary<uint256, Money> Amounts { get; }

	protected override ObservableCollection<HistoryItemViewModelBase> LoadChildren()
	{
		var result = new ObservableCollection<HistoryItemViewModelBase>();

		var balance = Balance ?? Money.Zero;

		for (var i = 0; i < CoinJoinTransactions.Count; i++)
		{
			var item = CoinJoinTransactions[i];

			var amount = Amounts[item.GetHash()];
			var transaction = new CoinJoinHistoryItemViewModel(
				UiContext,
				i,
				item,
				amount,
				_walletVm,
				balance,
				false);

			balance -= amount;

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

	public void Add(SmartTransaction item, Money amount)
	{
		if (!item.IsOwnCoinjoin())
		{
			throw new InvalidOperationException("Not a coinjoin item!");
		}

		CoinJoinTransactions.Insert(0, item);
		if (!Amounts.TryAdd(item.GetHash(), amount))
		{
			Amounts[item.GetHash()] += amount;
		}
		Refresh();
	}

	private void Refresh()
	{
		IsConfirmed = CoinJoinTransactions.All(x => x.Confirmed);
		ConfirmedToolTip = GetConfirmedToolTip(CoinJoinTransactions.Select(x => x.GetConfirmations((int)Services.BitcoinStore.SmartHeaderChain.ServerTipHeight)).Min());
		Date = CoinJoinTransactions.Select(tx => tx.FirstSeen).Max().ToLocalTime();

		SetAmount(
			CoinJoinTransactions.Sum(x => Amounts[x.GetHash()]),
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
