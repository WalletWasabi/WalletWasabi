using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private ModalDialogViewModelBase _modalDialog;
		private bool _canClose = true;

		public StatusBarViewModel StatusBar { get; }

		public MainWindowViewModel(StatusBarViewModel statusBar)
		{
			StatusBar = statusBar;
		}

		public static MainWindowViewModel Instance { get; internal set; }

		public async Task<bool> ShowDialogAsync(ModalDialogViewModelBase dialog)
		{
			ModalDialog = dialog;

			return await ModalDialog.ShowDialogAsync();
		}

		public ModalDialogViewModelBase ModalDialog
		{
			get { return _modalDialog; }
			private set { this.RaiseAndSetIfChanged(ref _modalDialog, value); }
		}

		public bool CanClose
		{
			get { return _canClose; }
			set { this.RaiseAndSetIfChanged(ref _canClose, value); }
		}
	}
}
