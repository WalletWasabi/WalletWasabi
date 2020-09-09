using ReactiveUI;
using Splat;
using System;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CrashReport.ViewModels
{
	public class CrashReportWindowViewModel : ViewModelBase
	{
		public CrashReportWindowViewModel()
		{
			var global = Locator.Current.GetService<Global>();
			CrashReporter = global.CrashReporter;

			OpenLogCommand = ReactiveCommand.CreateFromTask(async () => await FileHelpers.OpenFileInTextEditorAsync(Logger.FilePath));

			OkCommand = ReactiveCommand.Create(() =>
			{
				// This command is bound in xaml to close the window.
			});

			Observable
				.Merge(OpenLogCommand.ThrownExceptions)
				.Merge(OkCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private CrashReporter CrashReporter { get; }
		public int MinWidth => 520;
		public int MinHeight => 360;
		public string Title => "Wasabi Wallet - Crash Reporting";
		public string ReportTitle => "Wasabi has crashed";
		public string Details => $"Unfortunately, Wasabi has crashed. For more information, please open the log file. You may report this crash to the support team.{Environment.NewLine}{Environment.NewLine}Please always consider your privacy before sharing any information!{Environment.NewLine}{Environment.NewLine}Exception information:";
		public string Message => CrashReporter?.SerializedException?.Message;

		public ReactiveCommand<Unit, Unit> OpenLogCommand { get; }
		public ReactiveCommand<Unit, Unit> OkCommand { get; }
	}
}
