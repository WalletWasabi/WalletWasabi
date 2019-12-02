using NBitcoin;
using ReactiveUI;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private string _mnemonicWords;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, Mnemonic mnemonic) : base("Wallet Generated Successfully!")
		{
			_mnemonicWords = mnemonic.ToString();

			ConfirmCommand = ReactiveCommand.Create(() => owner.SelectTestPassword());

			ConfirmCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public string MnemonicWords
		{
			get => _mnemonicWords;
			set => this.RaiseAndSetIfChanged(ref _mnemonicWords, value);
		}

		public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();
		}
	}
}
