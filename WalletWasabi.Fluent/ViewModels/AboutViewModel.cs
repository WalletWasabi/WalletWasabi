using JetBrains.Annotations;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AboutViewModel : RoutableViewModel
	{
		public AboutViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget) : base(navigationState, navigationTarget)
		{
		}
	}
}