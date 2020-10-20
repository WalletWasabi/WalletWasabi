using System;
using System.Collections;
using AvalonStudio.MVVM;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagInputViewModel : ViewModelBase
    {
        private readonly ObservableAsPropertyHelper<IEnumerable> _suggestions;

        private string _inputText;
        private Action _EnterKeyOrSpacePressedAction;
        private Action _backspaceAndEmptyTextAction;
        private TagBoxViewModel _parent;

        public IEnumerable Suggestions => _suggestions.Value;

        public TagInputViewModel(TagBoxViewModel parent)
        {
            _parent = parent;
            _suggestions = _parent.WhenAnyValue(x => x.Suggestions)
                .ToProperty(this, x => x.Suggestions);

            this.EnterKeyOrSpacePressedAction += OnEnterKeyOrSpacePressedAction;
            this.BackspaceAndEmptyTextAction += OnBackspaceAndEmptyTextAction;
        }

        private void OnBackspaceAndEmptyTextAction()
        {
            _parent.RemoveTag();
        }

        private void OnEnterKeyOrSpacePressedAction()
        {
            _parent.AddTag(InputText.Trim());
        }

        public string InputText
        {
            get => _inputText;
            set => this.RaiseAndSetIfChanged(ref _inputText, value);
        }

        public Action EnterKeyOrSpacePressedAction
        {
            get => _EnterKeyOrSpacePressedAction;
            set => this.RaiseAndSetIfChanged(ref _EnterKeyOrSpacePressedAction, value);
        }

        public Action BackspaceAndEmptyTextAction
        {
            get => _backspaceAndEmptyTextAction;
            set => this.RaiseAndSetIfChanged(ref _backspaceAndEmptyTextAction, value);
        }
    }
}