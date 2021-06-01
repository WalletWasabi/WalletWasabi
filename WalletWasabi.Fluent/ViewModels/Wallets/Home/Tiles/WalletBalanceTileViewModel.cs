using System;
using System.Reactive;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceTileViewModel : TileViewModel
	{
		private readonly Wallet _wallet;
		private readonly IObservable<Unit> _balanceChanged;
		[AutoNotify] private string? _balanceBtc;
		[AutoNotify] private string? _balanceFiat;
		[AutoNotify] private string? _balancePrivateBtc;
		[AutoNotify] private string? _balanceNonPrivateBtc;

		public WalletBalanceTileViewModel(Wallet wallet, IObservable<Unit> balanceChanged)
		{
			_wallet = wallet;
			_balanceChanged = balanceChanged;
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_balanceChanged
				.Subscribe(_ => UpdateBalance())
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
	}
}