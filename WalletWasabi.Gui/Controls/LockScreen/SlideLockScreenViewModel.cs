using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLockScreenViewModel : ViewModelBase, ILockScreenViewModel
	{
		private LockScreenViewModel _parentVM;

		public SlideLockScreenViewModel(LockScreenViewModel lockScreenViewModel)
		{
			_parentVM = Guard.NotNull(nameof(lockScreenViewModel), lockScreenViewModel);
		}
		public void Dispose()
		{
			// empty.
		}
	}
}
