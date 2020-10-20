using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagBoxViewModel : ViewModelBase
    {
        private TagInputViewModel _tagInput;

        private ObservableAsPropertyHelper<IEnumerable<object>> _combinedContent;
        private ObservableCollection<TagViewModel> _tags;

        public TagBoxViewModel()
        {
            this.WhenAnyValue(x => x.Tags)
                .Where(x => x is { })
                .Subscribe(AddTagHandler);

            Tags = new ObservableCollection<TagViewModel>();
            TagInput = new TagInputViewModel(this);
        }

        public IEnumerable<object> CombinedContent => _combinedContent.Value;

        public ObservableCollection<TagViewModel> Tags
        {
            get => _tags;
            set => this.RaiseAndSetIfChanged(ref _tags, value);
        }

        public TagInputViewModel TagInput
        {
            get => _tagInput;
            set => this.RaiseAndSetIfChanged(ref _tagInput, value);
        }

        private void AddTagHandler(ObservableCollection<TagViewModel> tags)
        {
            _combinedContent = tags
                .ToObservableChangeSet(x => x)
                .ToCollection()
                .Select(x => x.Count != 0)
                .Merge(this.WhenAnyValue(x => x.Tags).Select(x => x is { }))
                .Merge(this.WhenAnyValue(x => x.TagInput).Select(x => x is { }))
                .Select(x => Tags.Append((object)TagInput))
                .ToProperty(this, x => x.CombinedContent);
        }

        public void AddTag()
        {
            Tags.Add(new TagViewModel(this, "Test"));
        }

        public void RemoveTag()
        {
            if (Tags.Count > 0)
                Tags.Remove(Tags.Last());
        }
    }
}