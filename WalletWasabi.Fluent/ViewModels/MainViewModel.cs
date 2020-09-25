using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ModalDialogHostViewModelBase
	{
		public MainViewModel()
		{
            base.SetHost(this);
		}
	}
}
