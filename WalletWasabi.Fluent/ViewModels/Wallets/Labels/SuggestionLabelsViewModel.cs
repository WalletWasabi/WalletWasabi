using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels
{
	public partial class SuggestionLabelsViewModel
	{
		private readonly SourceList<SuggestionLabelViewModel> _suggestionLabels;
		private readonly ObservableCollectionExtended<SuggestionLabelViewModel> _topSuggestions;
		private readonly ObservableCollectionExtended<string> _suggestions;
		private readonly ObservableCollectionExtended<string> _labels;
		private Action<string>? _addTag;

		public SuggestionLabelsViewModel(int topSuggestionsCount)
		{
			_labels = new ObservableCollectionExtended<string>();
			_suggestionLabels = new SourceList<SuggestionLabelViewModel>();
			_topSuggestions = new ObservableCollectionExtended<SuggestionLabelViewModel>();
			_suggestions = new ObservableCollectionExtended<string>();

			UpdateLabels();
			CreateSuggestions(topSuggestionsCount);

			SetAddTag = (addTag) => _addTag = addTag;
		}

		public ObservableCollection<SuggestionLabelViewModel> TopSuggestions => _topSuggestions;

		public ObservableCollection<string> Suggestions => _suggestions;

		public ObservableCollection<string> Labels => _labels;

		public Action<Action<string>>? SetAddTag { get; }

		public void UpdateLabels()
		{
			var labels = WalletHelpers.GetLabels();

			var mostUsedLabels = labels.GroupBy(x => x)
				.Select(x => new
				{
					Label = x.Key,
					Count = x.Count()
				})
				.OrderByDescending(x => x.Count)
				.ToList();

			_suggestionLabels.Clear();
			_suggestionLabels.AddRange(
				mostUsedLabels.Select(x => new SuggestionLabelViewModel(x.Label, x.Count, label => _addTag?.Invoke(label))));
		}

		private void CreateSuggestions(int topSuggestionsCount)
		{
			var suggestionLabelsFilter = this.WhenAnyValue(x => x.Labels).Select(_ => Unit.Default)
				.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).Select(_ => Unit.Default))
				.Select(_ => SuggestionLabelsFilter());

			_suggestionLabels
				.Connect()
				.Filter(suggestionLabelsFilter)
				.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Count))
				.Top(topSuggestionsCount)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(_topSuggestions)
				.Subscribe();

			_suggestionLabels
				.Connect()
				.Filter(suggestionLabelsFilter)
				.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Count))
				.Transform(x => x.Label)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(_suggestions)
				.Subscribe();

			Func<SuggestionLabelViewModel, bool> SuggestionLabelsFilter() => suggestionLabel => !_labels.Contains(suggestionLabel.Label);
		}
	}
}