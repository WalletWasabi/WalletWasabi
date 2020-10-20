using System.Collections;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagInputViewModel : ViewModelBase
    {
        private readonly TagBoxViewModel _parent;
        private IEnumerable _suggestions;

        public IEnumerable Suggestions
        {
            get => _suggestions;
            set => this.RaiseAndSetIfChanged(ref _suggestions, value);
        }

        public TagInputViewModel(TagBoxViewModel parent)
        {
            _parent = parent;
        }
    }
}