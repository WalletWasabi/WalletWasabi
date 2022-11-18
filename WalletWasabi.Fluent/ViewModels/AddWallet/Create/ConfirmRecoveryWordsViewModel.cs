using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Confirm Recovery Words")]
public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
{
	private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;
	private SourceList<RecoveryWordViewModel> _confirmationWordsSourceList;
	[AutoNotify] private bool _isSkipEnable;

	public ConfirmRecoveryWordsViewModel(
		List<RecoveryWordViewModel> mnemonicWords,
		Mnemonic mnemonic,
		string walletName)
	{
		_isSkipEnable = GetIsSkipEnabled();

		_confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();

		var nextCommandCanExecute =
			_confirmationWordsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.WhenValueChanged(x => x.IsConfirmed)
			.Select(_ => _confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

		_confirmationWordsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.WhenPropertyChanged(x => x.IsSelected)
			.Subscribe(x => OnWordSelected(x.Sender, x.Value));

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(() => OnNextAsync(mnemonic, walletName), nextCommandCanExecute);

		if (_isSkipEnable)
		{
			SkipCommand = ReactiveCommand.Create(() => NextCommand.Execute(null));
		}

		CancelCommand = ReactiveCommand.Create(OnCancel);

		_confirmationWordsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Sort(SortExpressionComparer<RecoveryWordViewModel>.Ascending(x => x.Index))
			.OnItemAdded(x => x.Reset())
			.Bind(out _confirmationWords)
			.Subscribe();

		_confirmationWordsSourceList.AddRange(mnemonicWords);

		AvailableWords = mnemonicWords.OrderBy(_ => Random.Shared.Next()).ToList();
	}

	public ReadOnlyObservableCollection<RecoveryWordViewModel> ConfirmationWords => _confirmationWords;

	public List<RecoveryWordViewModel> AvailableWords { get; }

	private void OnWordSelected(RecoveryWordViewModel selectedWord, bool isSelected)
	{
		if (isSelected)
		{
			var empty = ConfirmationWords.FirstOrDefault(x => x.SelectedWord is null);

			if (empty is { })
			{
				empty.SelectedWord = selectedWord.Word;
			}
		}
		else
		{
			var toRemove = ConfirmationWords.FirstOrDefault(x => x.SelectedWord == selectedWord.Word);

			if (toRemove is { })
			{
				for (int i = ConfirmationWords.IndexOf(toRemove); i < ConfirmationWords.Count; i++)
				{
					ConfirmationWords[i].SelectedWord =
						i < ConfirmationWords.Count - 1
						? ConfirmationWords[i + 1].SelectedWord
						: null;
				}
			}
		}
	}

	private async Task OnNextAsync(Mnemonic mnemonics, string walletName)
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Add Password", "This is needed to open and to recover your wallet. Store it safely, it cannot be changed.", enableEmpty: true),
			NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { } password)
		{
			IsBusy = true;

			var (km, mnemonic) = await Task.Run(
				() =>
				{
					var walletGenerator = new WalletGenerator(
						Services.WalletManager.WalletDirectories.WalletsDir,
						Services.WalletManager.Network)
					{
						TipHeight = Services.BitcoinStore.SmartHeaderChain.TipHeight
					};
					return walletGenerator.GenerateWallet(walletName, password, mnemonics);
				});
			IsBusy = false;
			await NavigateDialogAsync(new CoinJoinProfilesViewModel(km, true), NavigationTarget.DialogScreen);
		}
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
		if (!isInHistory)
		{
			_confirmationWordsSourceList.Dispose();
		}
	}

	private bool GetIsSkipEnabled()
	{
#if RELEASE
		return Services.WalletManager.Network != Network.Main || System.Diagnostics.Debugger.IsAttached;
#else
		return true;
#endif
	}
}
