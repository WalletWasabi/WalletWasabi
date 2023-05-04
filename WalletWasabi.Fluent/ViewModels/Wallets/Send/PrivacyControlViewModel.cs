using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly IEnumerable<SmartCoin>? _usedCoins;

	public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, IEnumerable<SmartCoin>? usedCoins)
	{
		_wallet = wallet;
		_transactionInfo = transactionInfo;
		_usedCoins = usedCoins;

		LabelSelection = new LabelSelectionViewModel(wallet.KeyManager, wallet.Kitchen.SaltSoup(), _transactionInfo);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => Complete(LabelSelection.GetUsedPockets()), LabelSelection.WhenAnyValue(x => x.EnoughSelected));
	}

	public LabelSelectionViewModel LabelSelection { get; }

	private void Complete(IEnumerable<Pocket> pockets)
	{
		var coins = Pocket.Merge(pockets.ToArray()).Coins;

		Close(DialogResultKind.Normal, coins);
	}

	private async Task InitializeLabelsAsync()
	{
		var privateThreshold = _wallet.AnonScoreTarget;

		await LabelSelection.ResetAsync(_wallet.Coins.GetPockets(privateThreshold).Select(x => new Pocket(x)).ToArray());
		await LabelSelection.SetUsedLabelAsync(_usedCoins, privateThreshold);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable
			.FromEventPattern(_wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
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

			IsBusy = false;
		});
	}
}
