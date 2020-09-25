using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	public abstract class DialogViewModelBase : ReactiveObject
	{
		public MainViewModel MainView { get; }

		public DialogViewModelBase(MainViewModel mainViewModel)
		{
			this.MainView = mainViewModel;
		}
	}
}
