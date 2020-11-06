using System.Collections.Generic;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CreateWallet
{
	public class RecoveryWordsViewModel : RoutableViewModel
	{
		public RecoveryWordsViewModel(
			NavigationStateViewModel navigationState,
			KeyManager keyManager,
			Mnemonic mnemonic,
			WalletManager walletManager)
			: base(navigationState, NavigationTarget.Dialog)
		{
			MnemonicWords = new List<RecoveryWordViewModel>();

			for (int i = 0; i < mnemonic.Words.Length; i++)
			{
				MnemonicWords.Add(new RecoveryWordViewModel(i + 1, mnemonic.Words[i]));
			}

			NextCommand = ReactiveCommand.Create(
				() => navigationState.DialogScreen?.Invoke().Router.Navigate.Execute(
					new ConfirmRecoveryWordsViewModel(navigationState, MnemonicWords, keyManager, walletManager)));

			CancelCommand = ReactiveCommand.Create(() => navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear());
		}

		public ICommand NextCommand { get; }

		public ICommand CancelCommand { get; }

		public List<RecoveryWordViewModel> MnemonicWords { get; set; }
	}
}