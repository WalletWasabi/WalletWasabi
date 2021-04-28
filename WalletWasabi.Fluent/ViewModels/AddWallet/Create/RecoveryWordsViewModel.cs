using System.Collections.Generic;
using System.Reactive.Disposables;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create
{
	[NavigationMetaData(Title = "Recovery words")]
	public partial class RecoveryWordsViewModel : RoutableViewModel
	{
		public RecoveryWordsViewModel(
			KeyManager keyManager,
			Mnemonic mnemonic,
			WalletManager walletManager)
		{
			MnemonicWords = new List<RecoveryWordViewModel>();

			for (int i = 0; i < mnemonic.Words.Length; i++)
			{
				MnemonicWords.Add(new RecoveryWordViewModel(i + 1, mnemonic.Words[i]));
			}

			EnableBack = true;

			NextCommand = ReactiveCommand.Create(() => OnNext(keyManager, walletManager));

			CancelCommand = ReactiveCommand.Create(OnCancel);
		}
		public List<RecoveryWordViewModel> MnemonicWords { get; set; }

		private void OnNext(KeyManager keyManager, WalletManager walletManager)
		{
			Navigate().To(new ConfirmRecoveryWordsViewModel(MnemonicWords, keyManager, walletManager));
		}

		private void OnCancel()
		{
			Navigate().Clear();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			var enableCancel = CurrentTarget != NavigationTarget.FullScreen;
			SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);

			base.OnNavigatedTo(isInHistory, disposables);
		}
	}
}
