using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomeViewModel : ViewModelBase, IRoutableViewModel
	{
		public HomeViewModel(IScreen screen) => HostScreen = screen;

		// Reference to IScreen that owns the routable view model.
		public IScreen HostScreen { get; }

		// Unique identifier for the routable view model.
		public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);		
	}
}
