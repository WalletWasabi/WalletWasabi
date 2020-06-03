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

        public string Title => "Wasabi Wallet - Crash Reporting";
        public SerializedException ReportedException => Global.CrashReportException;

		public Global Global { get; }
	}
}