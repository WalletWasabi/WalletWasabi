using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels;

public partial class SuggestionLabelsViewModel : ViewModelBase
{
	private readonly IWalletModel _wallet;
	private readonly SourceList<SuggestionLabelViewModel> _sourceLabels;
	private readonly ObservableCollectionExtended<string> _topSuggestions;
	private readonly ObservableCollectionExtended<string> _suggestions;
	private readonly ObservableCollectionExtended<string> _labels;
	[AutoNotify] private bool _isCurrentTextValid;

	public SuggestionLabelsViewModel(IWalletModel wallet, Intent intent, int topSuggestionsCount, IEnumerable<string>? labels = null)
	{
		_wallet = wallet;
		_sourceLabels = new SourceList<SuggestionLabelViewModel>();
		_topSuggestions = new ObservableCollectionExtended<string>();
		_suggestions = new ObservableCollectionExtended<string>();
		_labels = new ObservableCollectionExtended<string>(labels ?? new List<string>());
		Intent = intent;

		UpdateLabels();
		CreateSuggestions(topSuggestionsCount);
	}

	public ObservableCollection<string> TopSuggestions => _topSuggestions;

	public ObservableCollection<string> Suggestions => _suggestions;

	public ObservableCollection<string> Labels => _labels;

	public Intent Intent { get; }

	public void UpdateLabels()
	{
		var mostUsedLabels = _wallet.GetMostUsedLabels(Intent);
		_sourceLabels.Clear();
		_sourceLabels.AddRange(
			mostUsedLabels.Select(x => new SuggestionLabelViewModel(x.Label, x.Score)));
	}

	private void CreateSuggestions(int topSuggestionsCount)
	{
		var suggestionLabelsFilter = this.WhenAnyValue(x => x.Labels).ToSignal()
			.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).ToSignal())
			.Select(_ => SuggestionLabelsFilter());

		_sourceLabels
			.Connect()
			.Filter(suggestionLabelsFilter)
			.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Score).ThenByAscending(x => x.Label))
			.Top(topSuggestionsCount)
			.Transform(x => x.Label)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(_topSuggestions)
			.Subscribe();

		_sourceLabels
			.Connect()
			.Filter(suggestionLabelsFilter)
			.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Score).ThenByAscending(x => x.Label))
			.Transform(x => x.Label)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(_suggestions)
			.Subscribe();

		Func<SuggestionLabelViewModel, bool> SuggestionLabelsFilter() => suggestionLabel => !_labels.Contains(suggestionLabel.Label);
	}
}
