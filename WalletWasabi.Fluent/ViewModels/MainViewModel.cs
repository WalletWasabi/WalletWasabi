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

		DialogViewModelBase IDialogHost.CurrentDialog
		{
			get => _currentDialog;
			set => this.RaiseAndSetIfChanged(ref _currentDialog, value, nameof(IDialogHost.CurrentDialog));
		}

		public MainViewModel()
		{

		}

		void IDialogHost.ShowDialog<TDialog>(TDialog dialogViewModel)
		{
			(this as IDialogHost).CurrentDialog = dialogViewModel;
		}

		void IDialogHost.CloseDialog()
		{
			(this as IDialogHost).CurrentDialog = null;
		}
	}
}