using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using DynamicData.Binding;
using NBitcoin;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceTileViewModel : TileViewModel
	{
		private readonly Wallet _wallet;
		private readonly IObservable<Unit> _balanceChanged;
		private readonly ObservableCollection<HistoryItemViewModel> _history;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceBtc;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceFiat;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balancePrivateBtc;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _balanceNonPrivateBtc;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _recentTransactionName;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private DateTimeOffset? _recentTransactionDate;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _recentTransactionStatus;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _showRecentTransaction;

		public WalletBalanceTileViewModel(Wallet wallet, IObservable<Unit> balanceChanged, ObservableCollection<HistoryItemViewModel> history)
		{
			_wallet = wallet;
			_balanceChanged = balanceChanged;
			_history = history;
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_balanceChanged
				.Subscribe(_ => UpdateBalance())
				.DisposeWith(disposables);

			_history.ToObservableChangeSet()
				.Subscribe(_ => UpdateRecentTransaction())
				.DisposeWith(disposables);

			UpdateBalance();
		}

		private void UpdateBalance()
		{
			BalanceBtc = _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC)
				.FormattedBtc() + " BTC";

			BalanceFiat = _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC)
				.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");

			var privateThreshold = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
			var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold);
			var normalCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold);

			BalancePrivateBtc = privateCoins.TotalAmount().ToDecimal(MoneyUnit.BTC)
				.FormattedBtc() + " BTC";

			BalanceNonPrivateBtc = normalCoins.TotalAmount().ToDecimal(MoneyUnit.BTC)
				.FormattedBtc() + " BTC";
		}

		private void UpdateRecentTransaction()
		{
			var recent = _history.FirstOrDefault();
			if (recent is { })
			{
				var transactionSummary = recent.TransactionSummary;
				var confirmations = transactionSummary.Height.Type == HeightType.Chain ?
					(int)_wallet.BitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1
					: 0;
				var isConfirmed = confirmations > 0;
				var isIncoming = true;
				var amount = transactionSummary.Amount;
				if (amount < Money.Zero)
				{
					amount *= -1;
					isIncoming = false;
				}

				RecentTransactionName = isIncoming ? "Incoming" : "Outgoing";

				RecentTransactionDate = transactionSummary.DateTime.ToLocalTime();

				RecentTransactionStatus = $"{(amount.ToDecimal(MoneyUnit.BTC).FormattedBtc() + " BTC")}" +
				                          $"  -  {(isConfirmed ? "Confirmed" : "Pending")}";

				ShowRecentTransaction = true;
			}
			else
			{
				RecentTransactionName = default;
				RecentTransactionDate = default;
				RecentTransactionStatus = default;
				ShowRecentTransaction = false;
			}
		}
	}
}