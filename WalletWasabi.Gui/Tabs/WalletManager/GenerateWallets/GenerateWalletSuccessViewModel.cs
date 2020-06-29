using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private List<string> _mnemonicWords;
		private bool _isConfirmed;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, KeyManager keyManager, Mnemonic mnemonic) : base("Wallet Generated Successfully!")
		{
			_mnemonicWords = new List<string>(mnemonic.Words.Length);

			for (int i = 0; i < mnemonic.Words.Length; i++)
			{
				_mnemonicWords.Add($"{i + 1}. {mnemonic.Words[i]}");
			}

			var global = Locator.Current.GetService<Global>();

			ConfirmCommand = ReactiveCommand.Create(
				() =>
				{
					var wallet = global.WalletManager.AddWallet(keyManager);
					NotificationHelpers.Success("Wallet was generated.");
					owner.SelectTestPassword(wallet.WalletName);
				},
				this.WhenAnyValue(x => x.IsConfirmed));

			ConfirmCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public List<string> MnemonicWords
		{
			get => _mnemonicWords;
			set => this.RaiseAndSetIfChanged(ref _mnemonicWords, value);
		}

		public bool IsConfirmed
		{
			get => _isConfirmed;
			set => this.RaiseAndSetIfChanged(ref _isConfirmed, value);
		}

		public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
	}
}
