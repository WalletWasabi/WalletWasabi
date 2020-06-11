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
		public string Details => CrashReporter.GetException().ToString();
		public string Message => CrashReporter.GetException().Message;
	}
}
