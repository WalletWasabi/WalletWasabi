using Splat;

namespace WalletWasabi.Gui.CrashReport.ViewModels
{
	public class CrashReportWindowViewModel
	{
		public CrashReportWindowViewModel()
		{
			var global = Locator.Current.GetService<Global>();
			CrashReporter = global.CrashReporter;
		}

		private CrashReporter CrashReporter { get; }
		public int MinWidth => 370;
		public int MinHeight => 180;
		public string Title => "Wasabi Wallet - Crash Reporting";
		public string Details => "Wasabi has crashed. You can check the details here, open the log file, or report the crash to the support team. Please always consider your privacy before sharing any information!";
		public string Message => CrashReporter.GetException().Message;
	}
}
