using ReactiveUI;
using System;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels
{
	public class NavigationStateViewModel
	{
		public Func<IDialogHost> DialogHost { get; set; }
		public Func<IScreen> Screen { get; set; }
		public Func<IScreen> Dialog { get; set; }
		public Func<IRoutableViewModel> CancelView { get; set; }
		public Func<IRoutableViewModel> NextView { get; set; }
	}
}
