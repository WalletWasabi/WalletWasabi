using System.Collections.Generic;
using System.Reactive.Disposables;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Recovery Words")]
public partial class RecoveryWordsViewModel : RoutableViewModel
{
	private RecoveryWordsViewModel(WalletCreationOptions.AddNewWallet options)
	{
		var (_, _, mnemonic) = options;

		ArgumentNullException.ThrowIfNull(mnemonic);

		MnemonicWords = CreateList(mnemonic);

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
			result.Add(new RecoveryWordViewModel(i + 1, mnemonic.Words[i]));
		}

		return result;
	}
}
