using Splat;
using WalletWasabi.Gui.CrashReporter.Models;

namespace WalletWasabi.Gui.CrashReporter.ViewModels
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
        public string ReportedException => Global.CrashReportException.ToString();

		public Global Global { get; }
	}
}