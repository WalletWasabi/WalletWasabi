using System;
using System.Collections;
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
        private ObservableAsPropertyHelper<IEnumerable<object>> _combinedContent;
        private ObservableCollection<TagViewModel> _tags;
        public TagInputViewModel TagInput { get; }

        public TagBoxViewModel()
        {
            _tags = new ObservableCollection<TagViewModel>();

            TagInput = new TagInputViewModel(this);

            _combinedContent = Tags
                     .ToObservableChangeSet(x => x)
                     .ToCollection()
                     .Select(x => x.Append((object)TagInput))
                     .ToProperty(this, x => x.CombinedContent);
            
            // This is here just to activate/initialize the above rxui stuff. 
            Tags.Clear();
        }
        
        private IEnumerable _suggestions;

        public IEnumerable Suggestions
        {
            get => _suggestions;
            set => this.RaiseAndSetIfChanged(ref _suggestions, value);
        }


        public IEnumerable<object> CombinedContent => _combinedContent.Value;

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