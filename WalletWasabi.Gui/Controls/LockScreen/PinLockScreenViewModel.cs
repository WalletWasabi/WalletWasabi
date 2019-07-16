using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public class PinLockScreenViewModel : ViewModelBase, ILockScreenViewModel
    {
        private LockScreenViewModel _parentVM;

        public PinLockScreenViewModel(LockScreenViewModel lockScreenViewModel)
        {
            _parentVM = Guard.NotNull(nameof(lockScreenViewModel),lockScreenViewModel);
        }
    }
}
