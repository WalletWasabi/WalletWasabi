using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class BasicSendTabViewModel : SendControlViewModel
	{
		private Money _privateBalance;

		public BasicSendTabViewModel(Wallet wallet) : base(wallet, "Send")
		{
			Config = Locator.Current.GetService<Global>().Config;

			PrivateBalance = GetPrivateCoins().Sum(x => x.Amount);
		}

		public bool IsLurkingWifeMode => Global.UiConfig.LurkingWifeMode;

		public override string DoButtonText => "Preview Transaction";
		public override string DoingButtonText => "Building Transaction...";

		public Config Config { get; }

		public Money PrivateBalance
		{
			get => _privateBalance;
			set => this.RaiseAndSetIfChanged(ref _privateBalance, value);
		}

		public IEnumerable<SmartCoin> GetPrivateCoins() => Wallet.Coins.Where(x => x.AnonymitySet >= Config.MixUntilAnonymitySetValue);

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(PrivateBalance));
				this.RaisePropertyChanged(nameof(IsLurkingWifeMode));
			}).DisposeWith(disposables);

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(Config.WhenAnyValue(x => x.MixUntilAnonymitySet).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => PrivateBalance = GetPrivateCoins().Sum(x => x.Amount))
				.DisposeWith(disposables);
		}

		protected override async Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy, bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null)
		{
			BuildTransactionResult result = await Task.Run(() => Wallet.BuildTransaction(Password, payments, feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs));

			var txviewer = new TransactionViewerViewModel();
			IoC.Get<IShell>().AddDocument(txviewer);
			IoC.Get<IShell>().Select(txviewer);

			txviewer.Update(result);
			ResetUi();
			NotificationHelpers.Success("Transaction was built.");
		}
	}
}
