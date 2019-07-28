using Avalonia.Controls;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Dialogs;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.LockScreen;

namespace WalletWasabi.Gui.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private ModalDialogViewModelBase _modalDialog;
		private bool _canClose = true;

		private string _title = "Wasabi Wallet";

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		private double _height;

		public double Height
		{
			get => _height;
			internal set => this.RaiseAndSetIfChanged(ref _height, value);
		}

		private double _width;

		public double Width
		{
			get => _width;
			internal set => this.RaiseAndSetIfChanged(ref _width, value);
		}

		private WindowState _windowState;

		public WindowState WindowState
		{
			get => _windowState;
			internal set => this.RaiseAndSetIfChanged(ref _windowState, value);
		}

		private StatusBarViewModel _statusBar;

		public StatusBarViewModel StatusBar
		{
			get => _statusBar;
			internal set => this.RaiseAndSetIfChanged(ref _statusBar, value);
		}

		private LockScreenViewModel _lockScreen;

		public LockScreenViewModel LockScreen
		{
			get => _lockScreen;
			internal set => this.RaiseAndSetIfChanged(ref _lockScreen, value);
		}

		public ReactiveCommand<Unit, Unit> LockScreenCommand { get; }

		public MainWindowViewModel()
		{
			Shell = IoC.Get<IShell>();

			LockScreenCommand = ReactiveCommand.Create(() =>
			{
				Global.UiConfig.LockScreenActive = true;
			});

			LockScreenCommand.ThrownExceptions.Subscribe(Logging.Logger.LogWarning<MainWindowViewModel>);
		}

		public IShell Shell { get; }

		public static MainWindowViewModel Instance { get; internal set; }

		public async Task<bool> ShowDialogAsync(ModalDialogViewModelBase dialog)
		{
			ModalDialog = dialog;

			bool res = await ModalDialog.ShowDialogAsync();

			ModalDialog = null;

			return res;
		}

		public ModalDialogViewModelBase ModalDialog
		{
			get => _modalDialog;
			private set => this.RaiseAndSetIfChanged(ref _modalDialog, value);
		}

		public bool CanClose
		{
			get => _canClose;
			set => this.RaiseAndSetIfChanged(ref _canClose, value);
		}

		public Global Global { get; internal set; }
	}
}
