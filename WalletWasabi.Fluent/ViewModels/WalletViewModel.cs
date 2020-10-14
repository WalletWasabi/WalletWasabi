using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public class WalletViewModel : WalletViewModelBase
	{
		private ObservableCollection<ViewModelBase> _actions;

		protected WalletViewModel(IScreen screen, UiConfig uiConfig, Wallet wallet) : base(screen, wallet)
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Actions = new ObservableCollection<ViewModelBase>();

			uiConfig = Locator.Current.GetService<Global>().UiConfig;

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(uiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						var balance = Wallet.Coins.TotalAmount();
						Title = $"{WalletName} ({(uiConfig.PrivacyMode ? "#########" : balance.ToString(false, true))} BTC)";

						TitleTip = balance.ToUsdString(Wallet.Synchronizer.UsdExchangeRate, uiConfig.PrivacyMode);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			if (Wallet.KeyManager.IsHardwareWallet || !Wallet.KeyManager.IsWatchOnly)
			{
			}

			if (!Wallet.KeyManager.IsWatchOnly)
			{
			}
		}

		private CompositeDisposable Disposables { get; set; }

		public ObservableCollection<ViewModelBase> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		public override string IconName => "web_asset_regular";

		public static WalletViewModel Create(IScreen screen, UiConfig uiConfig, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletViewModel(screen, uiConfig, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new WatchOnlyWalletViewModel(screen, uiConfig, wallet)
					: new WalletViewModel(screen, uiConfig, wallet);
		}

		public void OpenWalletTabs()
		{
			// TODO: Implement.
		}
	}
}
