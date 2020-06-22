using ReactiveUI;
using Splat;
using System;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CrashReport.ViewModels
{
	public class CrashReportWindowViewModel : ViewModelBase
	{
		private bool _closeTrigger;

		public CrashReportWindowViewModel()
		{
			var global = Locator.Current.GetService<Global>();
			CrashReporter = global.CrashReporter;

			OpenLogCommand = ReactiveCommand.CreateFromTask(async () => await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath));

			OkCommand = ReactiveCommand.Create(() =>
			{
				CloseTrigger = true;
			});

			Observable
				.Merge(OpenLogCommand.ThrownExceptions)
				.Merge(OkCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private CrashReporter CrashReporter { get; }
		public int MinWidth => 520;
		public int MinHeight => 280;
		public string Title => "Wasabi Wallet - Crash Reporting";
		public string Details => $"Wasabi has crashed. You can check the details here, open the log file, or report the crash to the support team. {Environment.NewLine}{Environment.NewLine}Please always consider your privacy before sharing any information!";
		public string Message => CrashReporter?.SerializedException?.Message;

		public bool CloseTrigger
		{
			get => _closeTrigger;
			set => this.RaiseAndSetIfChanged(ref _closeTrigger, value);
		}

		public ReactiveCommand<Unit, Unit> OpenLogCommand { get; }
		public ReactiveCommand<Unit, Unit> OkCommand { get; }
	}
}
