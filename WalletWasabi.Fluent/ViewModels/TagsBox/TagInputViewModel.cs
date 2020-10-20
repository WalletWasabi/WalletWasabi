using System.Collections;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagInputViewModel : ViewModelBase
    {
        private readonly ObservableAsPropertyHelper<IEnumerable> _suggestions;

        private string _inputText;

        public IEnumerable Suggestions => _suggestions.Value;

        public TagInputViewModel(TagBoxViewModel parent)
        {
            _suggestions = parent.WhenAnyValue(x => x.Suggestions)
                .ToProperty(this, x => x.Suggestions);
        }

        public string InputText
        {
            get => _inputText;
            set => this.RaiseAndSetIfChanged(ref _inputText, value);
        }
    }
}