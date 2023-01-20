using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels;

public partial class SuggestionLabelsViewModel : ViewModelBase
{
	private readonly ObservableCollectionExtended<string> _labels;
	private readonly ObservableCollectionExtended<string> _suggestions;
	private readonly ObservableCollectionExtended<string> _topSuggestions;

	[AutoNotify] private bool _isCurrentTextValid;

	public SuggestionLabelsViewModel(KeyManager keyManager, Intent intent, int topSuggestionsCount, IEnumerable<string>? labels = null)
	{
		KeyManager = keyManager;
		Intent = intent;
		_topSuggestions = new ObservableCollectionExtended<string>();
		_suggestions = new ObservableCollectionExtended<string>();
		_labels = new ObservableCollectionExtended<string>(labels ?? new List<string>());

		CreateSuggestions(topSuggestionsCount);

		IsValid = Observable
			.Merge(this.WhenAnyValue(x => x.Labels.Count).ToSignal())
			.Merge(this.WhenAnyValue(x => x.IsCurrentTextValid).ToSignal())
			.Select(_ => Labels.Any() || IsCurrentTextValid);
	}

	public IObservable<bool> IsValid { get; }

	public ObservableCollection<string> TopSuggestions => _topSuggestions;

	public ObservableCollection<string> Suggestions => _suggestions;

	public ObservableCollection<string> Labels => _labels;

	private KeyManager KeyManager { get; }
	private Intent Intent { get; }

	private void CreateSuggestions(int topSuggestionsCount)
	{
		var ranking = LabelRanking.Rank(new RankInput(KeyManager.GetReceiveLabels(), WalletHelpers.GetReceiveAddressLabels(), WalletHelpers.GetChangeAddressLabels(), WalletHelpers.GetTransactionLabels()), Intent);
		var sourceLabels = ranking
			.Select(pair => new SuggestionLabelViewModel(pair.Key, pair.Value))
			.AsObservableChangeSet();

		var suggestionLabelsFilter = this.WhenAnyValue(x => x.Labels).ToSignal()
			.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).ToSignal())
			.Select(_ => SuggestionLabelsFilter());

		sourceLabels
			.Filter(suggestionLabelsFilter)
			.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Score).ThenByAscending(x => x.Label))
			.Top(topSuggestionsCount)
			.Transform(x => x.Label)
			.Bind(_topSuggestions)
			.Subscribe();

		sourceLabels
			.Filter(suggestionLabelsFilter)
			.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Score).ThenByAscending(x => x.Label))
			.Transform(x => x.Label)
			.Bind(_suggestions)
			.Subscribe();

		Func<SuggestionLabelViewModel, bool> SuggestionLabelsFilter()
		{
			return suggestionLabel => !_labels.Contains(suggestionLabel.Label);
		}
	}
}
