using ReactiveUI;
using System;

namespace WalletWasabi.Fluent.ViewModels
{
	public class NavigationStateViewModel
	{
		public Func<IScreen> Screen { get; set; }
		public Func<IScreen> Dialog { get; set; }
		public Func<IRoutableViewModel> CancelView { get; set; }
		public Func<IRoutableViewModel> NextView { get; set; }
	}
}
