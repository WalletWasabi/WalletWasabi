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

namespace WalletWasabi.Fluent.ViewModels
{
	public class WalletViewModel : WalletViewModelBase
	{
		private ObservableCollection<ViewModelBase> _actions;

		protected WalletViewModel(UiConfig uiConfig, Wallet wallet) : base(wallet)
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Actions = new ObservableCollection<ViewModelBase>();

			uiConfig = Locator.Current.GetService<Global>().UiConfig;

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(uiConfig.WhenAnyValue(x => x.LurkingWifeMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						var balance = Wallet.Coins.TotalAmount();
						Title = $"{WalletName} ({(uiConfig.LurkingWifeMode ? "#########" : balance.ToString(false, true))} BTC)";

						TitleTip = balance.ToUsdString(Wallet.Synchronizer.UsdExchangeRate, uiConfig.LurkingWifeMode);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			// If hardware wallet or not watch only wallet then we need the Send tab.
			if (Wallet.KeyManager.IsHardwareWallet || !Wallet.KeyManager.IsWatchOnly)
			{
				//SendTab = new SendTabViewModel(Wallet);
				//Actions.Add(SendTab);
			}

			//ReceiveTab = new ReceiveTabViewModel(Wallet);
			//HistoryTab = new HistoryTabViewModel(Wallet);

			//var advancedAction = new WalletAdvancedViewModel();
			//InfoTab = new WalletInfoViewModel(Wallet);
			//BuildTab = new BuildTabViewModel(Wallet);

			//Actions.Add(ReceiveTab);

			// If not watch only wallet (not hww) then we need the CoinJoin tab.
			if (!Wallet.KeyManager.IsWatchOnly)
			{
				//CoinjoinTab = new CoinJoinTabViewModel(Wallet);
				//Actions.Add(CoinjoinTab);
			}

			//Actions.Add(HistoryTab);

			//Actions.Add(advancedAction);
			//advancedAction.Items.Add(InfoTab);
			//advancedAction.Items.Add(BuildTab);
		}

		public static WalletViewModel Create(UiConfig uiConfig, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletViewModel(uiConfig, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new WatchOnlyWalletViewModel(uiConfig, wallet)
					: new WalletViewModel(uiConfig, wallet);
		}

		public ObservableCollection<ViewModelBase> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		private CompositeDisposable Disposables { get; set; }

		public void OpenWalletTabs()
		{
			// TODO: Implement.
			//var shell = IoC.Get<IShell>();

			//if (SendTab is { })
			//{
			//	shell.AddOrSelectDocument(SendTab);
			//}

			//shell.AddOrSelectDocument(ReceiveTab);

			//if (CoinjoinTab is { })
			//{
			//	shell.AddOrSelectDocument(CoinjoinTab);
			//}

			//shell.AddOrSelectDocument(HistoryTab);

			//SelectTab(shell);
		}

		//private void SelectTab(IShell shell)
		//{
		//	if (Wallet.Coins.Any())
		//	{
		//		WasabiDocumentTabViewModel tabToOpen = UiConfig.LastActiveTab switch
		//		{
		//			nameof(SendTabViewModel) => SendTab,
		//			nameof(ReceiveTabViewModel) => ReceiveTab,
		//			nameof(CoinJoinTabViewModel) => CoinjoinTab,
		//			nameof(BuildTabViewModel) => BuildTab,
		//			_ => HistoryTab
		//		};

		//		if (tabToOpen is { })
		//		{
		//			shell.AddOrSelectDocument(tabToOpen);
		//		}
		//	}
		//	else
		//	{
		//		shell.AddOrSelectDocument(ReceiveTab);
		//	}
		//}
	}
}
