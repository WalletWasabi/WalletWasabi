using System;
using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(Wallet wallet) : base(wallet)
		{
			PsbtWorkflowEnabled = Services.UiConfig.UsePsbtWorkflow;
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Services.UiConfig.WhenAnyValue(x => x.UsePsbtWorkflow)
				.Subscribe(value => PsbtWorkflowEnabled = value)
				.DisposeWith(disposables);
		}
	}
}
