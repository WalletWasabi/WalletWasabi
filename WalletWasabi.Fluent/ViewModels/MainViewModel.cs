using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels
{
	public class MainViewModel : ViewModelBase
	{
		private DialogViewModelBase _currentDialog;

		public DialogViewModelBase CurrentDialog
		{
			get => _currentDialog;
			private set => this.RaiseAndSetIfChanged(ref _currentDialog, value, nameof(CurrentDialog));
		}

		public MainViewModel()
		{

		}

		public void ShowDialog<TDialog>(TDialog dialogViewModel) where TDialog : DialogViewModelBase
		{
			CurrentDialog = dialogViewModel;
		}

		public void CloseDialog()
		{
			CurrentDialog = null;
		}
	}
}