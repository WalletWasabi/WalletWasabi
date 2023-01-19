using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
	[ObservableProperty] private bool _isSkipEnable;

	public ConfirmRecoveryWordsViewModel(
		List<RecoveryWordViewModel> mnemonicWords,
		Mnemonic mnemonic,
		string walletName)
	{
		_confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();
#if RELEASE
		_isSkipEnable = Services.WalletManager.Network != Network.Main || System.Diagnostics.Debugger.IsAttached;
#else
		_isSkipEnable = true;
#endif

		EnableBack = true;

		NextCommand = new AsyncRelayCommand(() => OnNextAsync(mnemonic, walletName), () => _confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

		// TODO RelayCommand: canExecute, refactor?
		_confirmationWordsSourceList
			.Connect()
			.WhenValueChanged(x => x.IsConfirmed)
			.Subscribe(_ => NextCommand.NotifyCanExecuteChanged());

		if (_isSkipEnable)
		{
			SkipCommand = new RelayCommand(() => NextCommand.Execute(null));
		}

		CancelCommand = new RelayCommand(OnCancel);

		_confirmationWordsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.OnItemAdded(x => x.Reset())
			.Sort(SortExpressionComparer<RecoveryWordViewModel>.Ascending(x => x.Index))
			.Bind(out _confirmationWords)
			.Subscribe();

		// Select random words to confirm.
		_confirmationWordsSourceList.AddRange(mnemonicWords.OrderBy(_ => Random.Shared.NextDouble()).Take(3));
	}

	public ReadOnlyObservableCollection<RecoveryWordViewModel> ConfirmationWords => _confirmationWords;

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
}
