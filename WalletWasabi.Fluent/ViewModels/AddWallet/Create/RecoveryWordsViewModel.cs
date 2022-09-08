using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Recovery Words")]
public partial class RecoveryWordsViewModel : RoutableViewModel
{
	public RecoveryWordsViewModel(Mnemonic mnemonic, string walletName)
	{
		MnemonicWords = new List<RecoveryWordViewModel>();

		for (int i = 0; i < mnemonic.Words.Length; i++)
		{
			MnemonicWords.Add(new RecoveryWordViewModel(i + 1, mnemonic.Words[i]));
		}

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => OnNext(mnemonic, walletName));

		CancelCommand = ReactiveCommand.Create(OnCancel);

		CopyToClipboardCommand = ReactiveCommand.CreateFromTask(CopyToClipboardAsync);
	}

	public List<RecoveryWordViewModel> MnemonicWords { get; set; }

	public ICommand CopyToClipboardCommand { get; }

	private void OnNext(Mnemonic mnemonic, string walletName)
	{
		Navigate().To(new ConfirmRecoveryWordsViewModel(MnemonicWords, mnemonic, walletName));
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	private async Task CopyToClipboardAsync()
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			var text =
				string.Join(Environment.NewLine, MnemonicWords.Select(x => x.ToString()));

			await clipboard.SetTextAsync(text);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}
}
