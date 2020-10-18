using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Gui.Controls.TransactionDetails.ViewModels;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public TransactionInfoTabViewModel(TransactionDetailsViewModel transaction, UiConfig uiConfig) : base(title: "")
		{
			Transaction = transaction;
			UiConfig = uiConfig;
			Title = $"Transaction ({transaction.TransactionId[0..10]}) Details";
		}

		public TransactionDetailsViewModel Transaction { get; }
		public UiConfig UiConfig { get; }

		public override void OnOpen(CompositeDisposable disposables)
		{
			UiConfig.WhenAnyValue(x => x.PrivacyMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Transaction.RaisePropertyChanged(nameof(Transaction.TransactionId)))
				.DisposeWith(disposables);

			base.OnOpen(disposables);
		}
	}
}
