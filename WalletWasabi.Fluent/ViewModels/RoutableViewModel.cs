using System;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class RoutableViewModel : ViewModelBase, IRoutableViewModel
	{
		protected RoutableViewModel(IScreen screen)
		{
			HostScreen = screen;
		}
 

		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

		public IScreen HostScreen { get; }
	}
}