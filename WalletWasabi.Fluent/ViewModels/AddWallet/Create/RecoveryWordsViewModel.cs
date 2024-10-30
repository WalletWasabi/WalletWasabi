using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "RecoveryWordsViewModel_Title")]
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
		CopyToClipboardCommand = ReactiveCommand.CreateFromTask(OnCopyToClipboardAsync);
	}

	public ICommand CopyToClipboardCommand { get; }

	public List<RecoveryWordViewModel> MnemonicWords { get; }

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		Navigate().To().ConfirmRecoveryWords(options, MnemonicWords);
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	private string GetRecoveryWordsString()
	{
		var words = MnemonicWords.Select(x => x.Word).ToArray();
		var text = string.Join(" ", words);

		return text;
	}

	private async Task OnCopyToClipboardAsync()
	{
		var text = GetRecoveryWordsString();

		await UiContext.Clipboard.SetTextAsync(text);
	}

	private async Task ClearRecoveryWordsFromClipboardAsync()
	{
		var currentText = await UiContext.Clipboard.GetTextAsync();
		var recoveryWordsString = GetRecoveryWordsString();

		if (currentText == recoveryWordsString)
		{
			await UiContext.Clipboard.ClearAsync();
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);

		Dispatcher.UIThread.InvokeAsync(ClearRecoveryWordsFromClipboardAsync);
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
