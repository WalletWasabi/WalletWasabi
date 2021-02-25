using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class WalletViewModel : WalletViewModelBase
	{
		protected WalletViewModel(UiConfig uiConfig, TransactionBroadcaster broadcaster, Wallet wallet) : base(wallet)
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			TransactionBroadcaster = broadcaster;

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(uiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					_ =>
				{
					try
					{
						var balance = Wallet.Coins.TotalAmount();
						Title = $"{WalletName} ({(uiConfig.PrivacyMode ? "#########" : balance.ToString(false))} BTC)";

						TitleTip = balance.ToUsdString(Wallet.Synchronizer.UsdExchangeRate, uiConfig.PrivacyMode);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);
		}

		private CompositeDisposable Disposables { get; set; }

		public override string IconName => "web_asset_regular";

		public TransactionBroadcaster TransactionBroadcaster { get; }

		public static WalletViewModel Create(UiConfig uiConfig, TransactionBroadcaster broadcaster, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletViewModel(uiConfig, broadcaster, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new WatchOnlyWalletViewModel(uiConfig, broadcaster, wallet)
					: new WalletViewModel(uiConfig, broadcaster,  wallet);
		}
	}
}
