using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagBoxViewModel : ViewModelBase
    {
        private readonly ReadOnlyObservableCollection<object> _combinedContent;


        private IEnumerable _suggestions;
        private ObservableCollection<TagViewModel> _tags;

        public TagBoxViewModel()
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
        }


        public ReadOnlyObservableCollection<object> CombinedContent => _combinedContent;

        public TagInputViewModel TagInput { get; }

        public IEnumerable Suggestions
        {
            get => _suggestions;
            set => this.RaiseAndSetIfChanged(ref _suggestions, value);
        }

        public ObservableCollection<TagViewModel> Tags
        {
            get => _tags;
            set => this.RaiseAndSetIfChanged(ref _tags, value);
        }

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