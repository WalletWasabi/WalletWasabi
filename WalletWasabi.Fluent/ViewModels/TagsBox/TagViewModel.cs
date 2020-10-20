using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagViewModel : ViewModelBase
    {
        private readonly TagBoxViewModel _parent;

        private string _tag;

        public TagViewModel(TagBoxViewModel parent, string tag)
        {
            _parent = parent;
            Tag = tag;
        }

        public string Tag
        {
            get => _tag;
            set => this.RaiseAndSetIfChanged(ref _tag, value);
        }
    }
}