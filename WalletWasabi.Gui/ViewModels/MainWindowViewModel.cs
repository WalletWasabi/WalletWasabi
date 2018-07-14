using AvalonStudio.Extensibility.Dialogs;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private ModalDialogViewModelBase _modalDialog;
		private bool _canClose = true;

		private string _title = "Wasabi Wallet";

		public string Title
		{
			get { return _title; }
			internal set { this.RaiseAndSetIfChanged(ref _title, value); }
		}

		private StatusBarViewModel _statusBar;

		public StatusBarViewModel StatusBar
		{
			get { return _statusBar; }
			internal set { this.RaiseAndSetIfChanged(ref _statusBar, value); }
		}

		public MainWindowViewModel()
		{
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
