using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Splat;
using WalletWasabi.Gui.Controls.TransactionDetails.ViewModels;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public TransactionInfoTabViewModel(TransactionDetailsViewModel transaction) : base("")
		{
			Global = Locator.Current.GetService<Global>();
			Transaction = transaction;
			Title = $"Transaction ({transaction.TransactionId[0..10]}) Details";
		}

		public override void OnOpen(CompositeDisposable disposables)
		{
			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Transaction.RaisePropertyChanged(nameof(Transaction.TransactionId)))
				.DisposeWith(disposables);

			base.OnOpen(disposables);
		}

		protected Global Global { get; }

		public TransactionDetailsViewModel Transaction { get; }
	}
}
