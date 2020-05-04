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
using System.Collections.Generic;

namespace WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private List<string> _mnemonicWords;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, KeyManager keyManager, Mnemonic mnemonic) : base("Wallet Generated Successfully!")
		{

			_mnemonicWords = new List<string>(mnemonic.Words.Length);

			for (int i = 0; i < mnemonic.Words.Length; i++)
			{
				_mnemonicWords.Add($"{i + 1}. {mnemonic.Words[i]}");
			}

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

		public List<string> MnemonicWords
		{
			get { return _mnemonicWords; }
			set { this.RaiseAndSetIfChanged(ref _mnemonicWords, value); }
		}

		public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
	}
}
