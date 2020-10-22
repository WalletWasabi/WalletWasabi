using System;
using System.Collections;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagInputViewModel : ViewModelBase
    {
        private readonly TagsBoxViewModel _parent;
        private readonly ObservableAsPropertyHelper<IEnumerable> _suggestions;
        private readonly ObservableAsPropertyHelper<bool> _restrictInputToSuggestions;

        private Action _backspaceAndEmptyTextAction;
        private Action<string> _commitTextAction;
        private Action _grabFocusAction;

        public TagInputViewModel(TagsBoxViewModel parent)
        {
            _parent = parent;
            _suggestions = _parent.WhenAnyValue(x => x.Suggestions)
                .ToProperty(this, x => x.Suggestions);
            
            _restrictInputToSuggestions = _parent.WhenAnyValue(x => x.RestrictInputToSuggestions)
                .ToProperty(this, x => x.RestrictInputToSuggestions);
            
            CommitTextAction += OnCommitTextAction;
            BackspaceAndEmptyTextAction += OnBackspaceAndEmptyTextAction;
        }

        public IEnumerable Suggestions => _suggestions.Value;
        public bool RestrictInputToSuggestions => _restrictInputToSuggestions.Value;
        
        public Action<string> CommitTextAction
        {
            get => _commitTextAction;
            set => this.RaiseAndSetIfChanged(ref _commitTextAction, value);
        }

        public Action BackspaceAndEmptyTextAction
        {
            get => _backspaceAndEmptyTextAction;
            set => this.RaiseAndSetIfChanged(ref _backspaceAndEmptyTextAction, value);
        }

        public Action GrabFocusAction
        {
            get => _grabFocusAction;
            set => this.RaiseAndSetIfChanged(ref _grabFocusAction, value);
        }

        private void OnBackspaceAndEmptyTextAction()
        {
            _parent.RemoveTag();
        }

        private void OnCommitTextAction(string tagString)
        {
            _parent.AddTag(tagString);
        }
    }
}