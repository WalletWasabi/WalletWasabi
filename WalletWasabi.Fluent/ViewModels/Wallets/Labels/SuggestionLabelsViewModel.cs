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
		private Action<string>? _addTag;
		private readonly SourceList<SuggestionLabelViewModel> _suggestionLabels;
		private readonly ObservableCollectionExtended<SuggestionLabelViewModel> _suggestionLabelResults;
		private readonly ObservableCollectionExtended<string> _labels;
		[AutoNotify] private HashSet<string> _suggestions;

		public SuggestionLabelsViewModel()
		{
			_labels = new ObservableCollectionExtended<string>();
			var allLabels = WalletHelpers.GetLabels();

			var mostUsedLabels = allLabels.GroupBy(x => x)
				.Select(x => new
				{
					Label = x.Key,
					Count = x.Count()
				})
				.OrderByDescending(x => x.Count)
				.ToList();

			_suggestions = mostUsedLabels.Select(x => x.Label).ToHashSet();

			_suggestionLabels = new SourceList<SuggestionLabelViewModel>();
			_suggestionLabelResults = new ObservableCollectionExtended<SuggestionLabelViewModel>();

			_suggestionLabels.AddRange(
				mostUsedLabels.Select(x => new SuggestionLabelViewModel(x.Label, x.Count, label => _addTag?.Invoke(label))));

			var suggestionLabelsFilter = this.WhenAnyValue(x => x.Labels).Select(_ => Unit.Default)
				.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).Select(_ => Unit.Default))
				.Select(SuggestionLabelsFilter);

			_suggestionLabels
				.Connect()
				.Filter(suggestionLabelsFilter)
				.Sort(SortExpressionComparer<SuggestionLabelViewModel>.Descending(x => x.Count))
				.Top(3)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(_suggestionLabelResults)
				.Subscribe();

			SetAddTag = (addTag) => _addTag = addTag;
		}

		public ObservableCollection<SuggestionLabelViewModel> SuggestionLabelResults => _suggestionLabelResults;

		public ObservableCollection<string> Labels => _labels;

		public Action<Action<string>>? SetAddTag { get; }

		private Func<SuggestionLabelViewModel, bool> SuggestionLabelsFilter(Unit unit)
		{
			return suggestionLabel => !_labels.Contains(suggestionLabel.Label);
		}
	}
}