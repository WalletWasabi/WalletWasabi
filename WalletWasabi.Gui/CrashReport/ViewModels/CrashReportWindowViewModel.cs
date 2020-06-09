using Splat;

namespace WalletWasabi.Gui.CrashReport.ViewModels
{
	public class CrashReportWindowViewModel
	{
		public CrashReportWindowViewModel()
		{
			Global = Locator.Current.GetService<Global>();
		}

		public int WinWidth => 640;
		public int WinHeight => 360;
		public string Title => "Wasabi Wallet - Crash Reporting";
		public string ReportedException => Global.CrashReporter.ToString();
		public Global Global { get; }
	}
}
