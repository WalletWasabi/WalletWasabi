using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagsBoxViewModel : ViewModelBase
    {
        private readonly ReadOnlyObservableCollection<object> _combinedContent;
        private IEnumerable _suggestions;
        private ObservableCollection<TagViewModel> _tags;

        private bool _restrictInputToSuggestions;
        private int _tagCountLimit = 12;

        public TagsBoxViewModel()
        {
            _tags = new ObservableCollection<TagViewModel>();

            TagInput = new TagInputViewModel(this);

            var list = new SourceList<object>();
            list.Add(TagInput);

            Tags.ToObservableChangeSet()
                .Cast(x => x as object)
                .Or(list.Connect())
                .Sort(SortExpressionComparer<object>.Ascending(i => i == TagInput ? 1 : 0))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _combinedContent)
                .AsObservableList();

            GrabFocusCommand = ReactiveCommand.Create((object x) =>
            {
                TagInput?.GrabFocusAction?.Invoke();
                return new object();
            });
        }

        public ReadOnlyObservableCollection<object> CombinedContent => _combinedContent;

        public TagInputViewModel TagInput { get; }

        public bool RestrictInputToSuggestions
        {
            get => _restrictInputToSuggestions;
            set => this.RaiseAndSetIfChanged(ref _restrictInputToSuggestions, value);
        }

        public IEnumerable Suggestions
        {
            get => _suggestions;
            set => this.RaiseAndSetIfChanged(ref _suggestions, value);
        }

        public IReadOnlyList<string> GetTagsAsListOfStrings()
        {
            return Tags.Select(x => x.Tag).ToImmutableList();
        }

        public int TagCountLimit
        {
            get => _tagCountLimit;
            set => this.RaiseAndSetIfChanged(ref _tagCountLimit, value);
        }
        
        public string GetTagsAsConcatString()
        {
            return string.Join(' ', Tags.Select(x => x.Tag));
        }

        public ObservableCollection<TagViewModel> Tags
        {
            get => _tags;
            set => this.RaiseAndSetIfChanged(ref _tags, value);
        }

        public ReactiveCommand<object, object> GrabFocusCommand { get; }

        public void AddTag(string tagString)
        {
            Tags.Add(new TagViewModel(this, tagString));
        }

        public void RemoveTag()
        {
            if (Tags.Any())
            {
                Tags.Remove(Tags.Last());
            }
        }
    }
}