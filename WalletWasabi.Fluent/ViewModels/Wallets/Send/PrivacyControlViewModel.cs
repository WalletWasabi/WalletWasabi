using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly IWalletModel _walletModel;
	private readonly SendFlowModel _sendFlow;
	private readonly TransactionInfo _transactionInfo;
	private readonly bool _isSilent;
	private readonly IEnumerable<SmartCoin>? _usedCoins;

	public PrivacyControlViewModel(UiContext uiContext, IWalletModel walletModel, SendFlowModel sendFlow, TransactionInfo transactionInfo, IEnumerable<SmartCoin>? usedCoins, bool isSilent) : base(uiContext)
	{
		_walletModel = walletModel;
		_sendFlow = sendFlow;
		_transactionInfo = transactionInfo;
		_isSilent = isSilent;
		_usedCoins = usedCoins;

		LabelSelection = new LabelSelectionViewModel(uiContext, walletModel, _transactionInfo, isSilent);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => Complete(LabelSelection.GetUsedPockets()), LabelSelection.WhenAnyValue(x => x.EnoughSelected));

		IsBusy = true;
	}

	public LabelSelectionViewModel LabelSelection { get; }

	private void Complete(IEnumerable<Pocket> pockets)
	{
		var coins = Pocket.Merge(pockets.ToArray()).Coins;

		Close(DialogResultKind.Normal, coins);
	}

	private async Task InitializeLabelsAsync()
	{
		var privateThreshold = _walletModel.AnonScoreTarget;

		var coinsToExclude = UiContext.Services.GetCoinsInCriticalPhase(_walletModel.Id).ToList();

		var pockets = _sendFlow.GetPockets();

		await LabelSelection.ResetAsync(pockets, coinsToExclude);
		await LabelSelection.SetUsedLabelAsync(_usedCoins, privateThreshold);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		// TODO: Decoupling
		UiContext.Services.SubscribeToEvent<WalletRelevantTransactionProcessed>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.SubscribeAsync(_ => InitializeLabelsAsync())
			.DisposeWith(disposables);

		Dispatcher.UIThread.InvokeAsync(async () =>
		{
			IsBusy = true;

			if (!isInHistory)
			{
				await InitializeLabelsAsync();
			}

			if (_isSilent)
			{
				var autoSelectedPockets = await LabelSelection.AutoSelectPocketsAsync();

				Complete(autoSelectedPockets);
			}

			IsBusy = false;
		});
	}
}
