using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase, IDialogHost
	{
		private DialogViewModelBase _currentDialog;
		
		public MainViewModel()
		{
		}

		DialogViewModelBase IDialogHost.CurrentDialog => _currentDialog;

		private void SetDialog(DialogViewModelBase target)
		{
			this.RaiseAndSetIfChanged(ref _currentDialog, target, nameof(IDialogHost.CurrentDialog));
		}

		void IDialogHost.ShowDialog<TDialog>(TDialog dialogViewModel)
		{
			SetDialog(dialogViewModel);
		}

		void IDialogHost.CloseDialog()
		{
			SetDialog(null);
		}
	}
}