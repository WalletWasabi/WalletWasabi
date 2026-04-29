using System.Collections.Generic;
using System.Reactive.Disposables;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Recovery Words")]
public partial class RecoveryWordsViewModel : RoutableViewModel
{
	public RecoveryWordsViewModel(UiContext uiContext, WalletCreationOptions.AddNewWallet options) : base(uiContext)
	{
		var recoveryWordsBackup = options.SelectedWalletBackup as RecoveryWordsBackup;

		ArgumentNullException.ThrowIfNull(recoveryWordsBackup);
		ArgumentNullException.ThrowIfNull(recoveryWordsBackup.Mnemonic);

		MnemonicWords = CreateList(recoveryWordsBackup.Mnemonic);

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => OnNext(options));

		CancelCommand = ReactiveCommand.Create(OnCancel);
	}

	public List<RecoveryWordViewModel> MnemonicWords { get; }

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		Navigate().To().ConfirmRecoveryWords(options, MnemonicWords);
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private List<RecoveryWordViewModel> CreateList(Mnemonic mnemonic)
	{
		var result = new List<RecoveryWordViewModel>();

		for (int i = 0; i < mnemonic.Words.Length; i++)
		{
			result.Add(new RecoveryWordViewModel(UiContext, i + 1, mnemonic.Words[i]));
		}

		return result;
	}
}
