using ReactiveUI;
using System;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels
{
	public class NavigationStateViewModel
	{
		public Func<IScreen> HomeScreen { get; set; }
		public Func<IScreen> DialogScreen { get; set; }
		public Func<IDialogHost> DialogHost { get; set; }
		public Func<IRoutableViewModel> CancelView { get; set; }
		public Func<IRoutableViewModel> NextView { get; set; }
	}
}
