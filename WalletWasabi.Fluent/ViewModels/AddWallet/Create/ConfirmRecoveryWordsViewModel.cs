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
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Confirm Recovery Words")]
public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
{
	private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;
	private SourceList<RecoveryWordViewModel> _confirmationWordsSourceList;
	[AutoNotify] private bool _isSkipEnable;

	public ConfirmRecoveryWordsViewModel(List<RecoveryWordViewModel> mnemonicWords, KeyManager keyManager)
	{
		_confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();
		_isSkipEnable = Services.WalletManager.Network != Network.Main || System.Diagnostics.Debugger.IsAttached;

		var nextCommandCanExecute =
			_confirmationWordsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.WhenValueChanged(x => x.IsConfirmed)
			.Select(_ => _confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(() => OnNextAsync(keyManager), nextCommandCanExecute);

		if (_isSkipEnable)
		{
			SkipCommand = ReactiveCommand.Create(() => NextCommand.Execute(null));
		}

		CancelCommand = ReactiveCommand.Create(OnCancel);

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

	private async Task OnNextAsync(KeyManager keyManager)
	{
		await NavigateDialogAsync(new CoinJoinProfilesViewModel(keyManager, true), NavigationTarget.DialogScreen);
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
