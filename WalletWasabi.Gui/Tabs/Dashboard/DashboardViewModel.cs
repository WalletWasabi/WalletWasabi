using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.Dashboard
{
	[Export]
	[Shared]
	public class DashboardViewModel : WasabiDocumentTabViewModel
	{
	

		public DashboardViewModel() : base("Dashboard")
		{
		}


	}
}
