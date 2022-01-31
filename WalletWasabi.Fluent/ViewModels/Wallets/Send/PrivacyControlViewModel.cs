using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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

		LabelSelection = new LabelSelectionViewModel(_transactionInfo.Amount);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => Complete(LabelSelection.GetUsedPockets()));
	}

	public LabelSelectionViewModel LabelSelection { get; }

	private void Complete(IEnumerable<Pocket> pockets)
	{
		var coins = pockets.SelectMany(x => x.Coins);

		Close(DialogResultKind.Normal, coins);
	}

	private void InitializeLabels()
	{
		LabelSelection.Reset(_wallet.Coins.GetPockets(_wallet.ServiceConfiguration.MinAnonScoreTarget).Select(x => new Pocket(x)).ToArray());
		LabelSelection.SetUsedLabel(_usedCoins);
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
			var usedPockets = LabelSelection.GetUsedPockets();

			if (usedPockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket &&
			    privatePocket.Amount >= _transactionInfo.Amount)
			{
				Complete(usedPockets.Where(x => x.Labels == CoinPocketHelper.PrivateFundsText));
			}
			else if (usedPockets.Where(x => x.Labels != CoinPocketHelper.PrivateFundsText).Sum(x => x.Amount) >= _transactionInfo.Amount)
			{
				Complete(usedPockets.Where(x => x.Labels != CoinPocketHelper.PrivateFundsText));
			}
			else
			{
				Complete(usedPockets);
			}
		}
	}
}
