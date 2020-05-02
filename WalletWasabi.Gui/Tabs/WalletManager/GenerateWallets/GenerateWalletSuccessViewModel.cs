using NBitcoin;
using ReactiveUI;
using System.Reactive;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using System;
using System.Reactive.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Models;
using WalletWasabi.Gui.Helpers;
using Splat;

namespace WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private string _mnemonicWords;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, KeyManager keyManager, Mnemonic mnemonic) : base("Wallet Generated Successfully!")
		{
			_mnemonicWords = mnemonic.ToString();
			var global = Locator.Current.GetService<Global>();

			ConfirmCommand = ReactiveCommand.Create(() =>
			{
				var wallet = global.WalletManager.AddWallet(keyManager);
				NotificationHelpers.Success("Wallet was generated.");
				owner.SelectTestPassword(wallet.WalletName);
			});

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
