using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomeViewModel : ViewModelBase
	{
		public HomeViewModel(IScreen screen)
		{
			HostScreen = screen;
		}
	}
}
