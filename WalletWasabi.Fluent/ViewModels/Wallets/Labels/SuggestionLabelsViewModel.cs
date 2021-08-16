using System;
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
	public class SuggestionLabelsViewModel : ViewModelBase
	{
		private readonly SourceList<SuggestionLabelViewModel> _sourceLabels;
		private readonly ObservableCollectionExtended<string> _topSuggestions;
		private readonly ObservableCollectionExtended<string> _suggestions;
		private readonly ObservableCollectionExtended<string> _labels;

		public SuggestionLabelsViewModel(int topSuggestionsCount)
		{
			_sourceLabels = new SourceList<SuggestionLabelViewModel>();
			_topSuggestions = new ObservableCollectionExtended<string>();
			_suggestions = new ObservableCollectionExtended<string>();
			_labels = new ObservableCollectionExtended<string>();

			UpdateLabels();
			CreateSuggestions(topSuggestionsCount);
		}

		public ObservableCollection<string> TopSuggestions => _topSuggestions;

		public ObservableCollection<string> Suggestions => _suggestions;

		public ObservableCollection<string> Labels => _labels;

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

			_sourceLabels.Clear();
			_sourceLabels.AddRange(
				mostUsedLabels.Select(x => new SuggestionLabelViewModel(x.Label, x.Count)));
		}

		private void CreateSuggestions(int topSuggestionsCount)
		{
			var suggestionLabelsFilter = this.WhenAnyValue(x => x.Labels).Select(_ => Unit.Default)
				.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).Select(_ => Unit.Default))
				.Select(_ => SuggestionLabelsFilter());

			_sourceLabels
				.Connect()
				.Filter(suggestionLabelsFilter)
				.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Count).ThenByAscending(x => x.Label))
				.Top(topSuggestionsCount)
				.Transform(x => x.Label)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(_topSuggestions)
				.Subscribe();

			_sourceLabels
				.Connect()
				.Filter(suggestionLabelsFilter)
				.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Count).ThenByAscending(x => x.Label))
				.Transform(x => x.Label)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(_suggestions)
				.Subscribe();

			Func<SuggestionLabelViewModel, bool> SuggestionLabelsFilter() => suggestionLabel => !_labels.Contains(suggestionLabel.Label);
		}
	}
}