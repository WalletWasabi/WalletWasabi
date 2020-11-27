using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class RecoveryWordsViewModel : RoutableViewModel
	{
		public RecoveryWordsViewModel(
			NavigationStateViewModel navigationState,
			KeyManager keyManager,
			Mnemonic mnemonic,
			WalletManager walletManager)
			: base(navigationState, NavigationTarget.DialogScreen)
		{
			MnemonicWords = new List<RecoveryWordViewModel>();

			for (int i = 0; i < mnemonic.Words.Length; i++)
			{
				MnemonicWords.Add(new RecoveryWordViewModel(i + 1, mnemonic.Words[i]));
			}

			NextCommand = ReactiveCommand.Create(
				() => NavigateTo(new ConfirmRecoveryWordsViewModel(navigationState, MnemonicWords, keyManager, walletManager), NavigationTarget.DialogScreen));

			CancelCommand = ReactiveCommand.Create(() => ClearNavigation(NavigationTarget.DialogScreen));
		}

		public List<RecoveryWordViewModel> MnemonicWords { get; set; }
	}
}