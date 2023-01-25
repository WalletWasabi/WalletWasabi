using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Recipient")]
public partial class LabelEntryDialogViewModel : DialogViewModelBase<SmartLabel?>
{
	private readonly Wallet _wallet;

	public LabelEntryDialogViewModel(Wallet wallet, SmartLabel label)
	{
		_wallet = wallet;
		SuggestionLabels = new SuggestionLabelsViewModel(wallet.KeyManager, Intent.Send, 3)
		{
			Labels = { label.Labels }
		};

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = new RelayCommand(OnNext, () => SuggestionLabels.Labels.Any() || SuggestionLabels.IsCurrentTextValid);

		// TODO RelayCommand: Discuss this with Dan
		SuggestionLabels.WhenAnyValue(x => x.Labels.Count).Subscribe(_ => NextCommand.NotifyCanExecuteChanged());
	}

	public SuggestionLabelsViewModel SuggestionLabels { get; }

	private void OnNext()
	{
		Close(DialogResultKind.Normal, new SmartLabel(SuggestionLabels.Labels.ToArray()));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.TransactionProcessor.WhenAnyValue(x => x.Coins)
			.Select(_ => Unit.Default)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => SuggestionLabels.UpdateLabels())
			.DisposeWith(disposables);
	}

	protected override void OnActivated()
	{
		base.OnActivated();

		Messenger.Register<LabelEntryDialogViewModel, PropertyChangedMessage<bool>>(this, (r, m) =>
		{
			r.NextCommand?.NotifyCanExecuteChanged();
		});
	}
}
