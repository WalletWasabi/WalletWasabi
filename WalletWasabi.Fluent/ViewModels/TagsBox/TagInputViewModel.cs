using System;
using System.Collections;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagInputViewModel : ViewModelBase
    {
        private readonly TagBoxViewModel _parent;
        private readonly ObservableAsPropertyHelper<IEnumerable> _suggestions;
        private Action _backspaceAndEmptyTextAction;
        private Action<string> _commitTextAction;

        public TagInputViewModel(TagBoxViewModel parent)
        {
            _parent = parent;
            _suggestions = _parent.WhenAnyValue(x => x.Suggestions)
                .ToProperty(this, x => x.Suggestions);

            CommitTextAction += OnCommitTextAction;
            BackspaceAndEmptyTextAction += OnBackspaceAndEmptyTextAction;
        }

        public IEnumerable Suggestions => _suggestions.Value;

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