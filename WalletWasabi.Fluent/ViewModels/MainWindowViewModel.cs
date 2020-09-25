using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Popup;

namespace WalletWasabi.Fluent.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IPopupHost
    {
        public string Greeting => "Hello World!";

        private IPopupView _currentPopupView;
        
        private readonly ObservableAsPropertyHelper<bool> _canDisplayDialog;
        bool IPopupHost.CanDisplayDialog => _canDisplayDialog?.Value ?? false;

        public MainWindowViewModel()
        {
            base.SetHost(this);
            _canDisplayDialog = this
                .WhenAnyValue(x => x.CurrentPopupView)
                .Select(x=>!(x is null))
                .ToProperty(this, x => x.CanDisplayDialog);
        }

        public IPopupView CurrentPopupView
        {
            get => _currentPopupView;
            set => this.RaiseAndSetIfChanged(ref _currentPopupView, value, nameof(CurrentPopupView));
        }

        public void SetDialog(IPopupView targetView)
        {
            if (CanDisplayDialog)
            {
                targetView.Parent = this;
                CurrentPopupView = targetView;
            }
        }
        
        public void Close()
        {
            CurrentPopupView = null;
        }
    }
}
