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

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private string _mnemonicWords;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, string password, string walletFilePath, Global global) : base("Wallet Generated Successfully!")
		{
			// Here we are not letting anything that will be autocorrected later. We need to generate the wallet exactly with the entered password bacause of compatibility.
			PasswordHelper.Guard(password);

			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, password);
			_mnemonicWords = mnemonic.ToString();

			ConfirmCommand = ReactiveCommand.Create(() =>
				{
					DoConfirmCommand(keyManager, walletFilePath, global);
					owner.SelectTestPassword();
				});

			ConfirmCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void DoConfirmCommand(KeyManager keyManager, string walletFilePath, Global global)
		{
			keyManager.SetNetwork(global.Network);
			keyManager.SetBestHeight(new Height(global.BitcoinStore.SmartHeaderChain.TipHeight));
			keyManager.SetFilePath(walletFilePath);
			keyManager.ToFile();

			NotificationHelpers.Success("Wallet is successfully generated!");
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
