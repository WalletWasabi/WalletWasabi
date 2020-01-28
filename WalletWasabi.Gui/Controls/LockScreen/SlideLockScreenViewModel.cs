namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLockScreenViewModel : LockScreenViewModelBase
	{
		public readonly double ThresholdPercent = 1 / 6d;

		public SlideLockScreenViewModel()
		{
			CanSlide = true;
		}		
	}
}
