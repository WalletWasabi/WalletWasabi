using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	public class DialogViewModelBase : ReactiveObject
	{
		private MainViewModel MainView { get; }

		public DialogViewModelBase(MainViewModel mainViewModel)
		{
			this.MainView = mainViewModel;
		}

		public void Close()
		{
			MainView?.CloseDialog();
		}
	}
}
