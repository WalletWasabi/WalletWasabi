using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly bool _isSilent;
	private readonly IEnumerable<SmartCoin>? _usedCoins;

	public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, IEnumerable<SmartCoin>? usedCoins, bool isSilent)
	{
		_wallet = wallet;
		_transactionInfo = transactionInfo;
		_isSilent = isSilent;
		_usedCoins = usedCoins;

		LabelSelection = new LabelSelectionViewModel(wallet.KeyManager, wallet.Kitchen.SaltSoup(), _transactionInfo, isSilent);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = new RelayCommand(() => Complete(LabelSelection.GetUsedPockets()), () => LabelSelection.EnoughSelected);

		// TODO RelayCommand, refactor
		this.WhenAnyValue(x => x.LabelSelection.EnoughSelected)
			.Subscribe(_ => NextCommand.NotifyCanExecuteChanged());
	}

	public LabelSelectionViewModel LabelSelection { get; }

	private void Complete(IEnumerable<Pocket> pockets)
	{
		var coins = Pocket.Merge(pockets.ToArray()).Coins;

		Close(DialogResultKind.Normal, coins);
	}

	private void InitializeLabels()
	{
		var privateThreshold = _wallet.AnonScoreTarget;

		LabelSelection.Reset(_wallet.Coins.GetPockets(privateThreshold).Select(x => new Pocket(x)).ToArray());
		LabelSelection.SetUsedLabel(_usedCoins, privateThreshold);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!isInHistory)
		{
			InitializeLabels();
		}

		Observable
			.FromEventPattern(_wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => InitializeLabels())
			.DisposeWith(disposables);

		if (_isSilent)
		{
			var autoSelectedPockets = LabelSelection.AutoSelectPockets();

			Complete(autoSelectedPockets);
		}
	}
}
