using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Recipient", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class LabelEntryDialogViewModel : DialogViewModelBase<LabelsArray?>
{
	private readonly IWalletModel _wallet;

	public LabelEntryDialogViewModel(UiContext uiContext, IWalletModel wallet, LabelsArray labels)
	{
		_wallet = wallet;
		UiContext = uiContext;

		SuggestionLabels = new SuggestionLabelsViewModel(uiContext, wallet, Intent.Send, 3)
		{
			Labels = { labels.AsEnumerable() }
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
		SuggestionLabels.ForceAdd = true;
		Close(DialogResultKind.Normal, new LabelsArray(SuggestionLabels.Labels.ToArray()));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Coins.List
			.Connect()
			.ToSignal()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => SuggestionLabels.UpdateLabels())
			.DisposeWith(disposables);
	}
}
