using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
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

		var nextCommandCanExecute =
			Observable
				.Merge(SuggestionLabels.WhenAnyValue(x => x.Labels.Count).ToSignal())
				.Merge(SuggestionLabels.WhenAnyValue(x => x.IsCurrentTextValid).ToSignal())
				.Select(_ => SuggestionLabels.Labels.Any() || SuggestionLabels.IsCurrentTextValid);

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);
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
			.ToSignal()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => SuggestionLabels.UpdateLabels())
			.DisposeWith(disposables);
	}
}
