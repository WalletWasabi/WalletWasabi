using ReactiveUI; 
using System.Reactive.Disposables; 
using WalletWasabi.Gui.Models; 

namespace WalletWasabi.Gui.ViewModels
{
    public class LockScreenViewModel : ViewModelBase
    {
        private CompositeDisposable Disposables { get; } = new CompositeDisposable();

        public Global Global { get; }

        public LockScreenViewModel(Global global)
        {
            Global = global;
        }

        private LockScreenType _lockScreenType;
        public LockScreenType ActiveLockScreen
        {
            get => _lockScreenType;
            set => this.RaiseAndSetIfChanged(ref _lockScreenType, value);
        }

        private bool _isLocked;
        public bool IsLocked
        {
            get => _isLocked;
            set => this.RaiseAndSetIfChanged(ref _isLocked, value);
        }

        public void Initialize()
        {
            Global.UiConfig.WhenAnyValue(x => x.LockScreenActive)
                           .BindTo(this, y => y.IsLocked)
                           .DisposeWith(Disposables);

            this.WhenAnyValue(x => x.IsLocked)
                .BindTo(Global.UiConfig, y => y.LockScreenActive)
                .DisposeWith(Disposables);

            Global.UiConfig.WhenAnyValue(x => x.LockScreenType)
                           .BindTo(this, y => y.ActiveLockScreen)
                           .DisposeWith(Disposables);
        }
    }
}