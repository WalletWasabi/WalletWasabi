using ReactiveUI;

namespace WalletWasabi.UI.Desktop.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        public string Greeting => "Hello World!";
/*        private ViewModelBase _content;
        public ViewModelBase Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }
*/
        public MainWindowViewModel()
        {
//            Content = new ViewModelBase();
        }
    }
}
