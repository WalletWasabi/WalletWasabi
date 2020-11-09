using System;
using ReactiveUI;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLockScreenViewModel : WasabiLockScreenViewModelBase
	{
		public SlideLockScreenViewModel() : base()
		{
			CanSlide = true;

			this.WhenAnyValue(x => x.IsLocked)
				.Where(x => !x)
				.Take(1)
				.Subscribe(x => Close());
		}
	}
}
