using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
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
		CopyToClipboardCommand = ReactiveCommand.CreateFromTask(OnCopyToClipboardAsync);
	}

	public ICommand CopyToClipboardCommand { get; }

	public List<RecoveryWordViewModel> MnemonicWords { get; set; }

	private void OnNext(Mnemonic mnemonic, string walletName)
	{
		Navigate().To(new ConfirmRecoveryWordsViewModel(MnemonicWords, mnemonic, walletName));
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	private async Task OnCopyToClipboardAsync()
	{
		if (Application.Current?.Clipboard is null)
		{
			return;
		}

		var words =
			MnemonicWords.Select(x => x.Word).ToArray();

		var text = string.Join(" ", words);

		await Application.Current.Clipboard.SetTextAsync(text);

		Observable.Timer(TimeSpan.FromSeconds(30))
				  .ObserveOn(RxApp.MainThreadScheduler)
				  .SubscribeAsync(async _ =>
					{
						var currentText = await Application.Current.Clipboard.GetTextAsync();
						if (currentText == text)
						{
							await Application.Current.Clipboard.ClearAsync();
						}
					});
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}
}
