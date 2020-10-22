using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.TagsBox
{
    public class TagViewModel : ViewModelBase
    {
        private readonly TagsBoxViewModel _parent;

        private string _tag;

        public TagViewModel(TagsBoxViewModel parent, string tag)
        {
            _parent = parent;
            _tag = tag;
        }

        public string Tag
        {
            get => _tag;
            set => this.RaiseAndSetIfChanged(ref _tag, value);
        }
    }
}