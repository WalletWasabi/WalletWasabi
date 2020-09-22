using Avalonia;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.CrashReport.Views
{
	public class CrashReportWindow : WasabiWindow
	{
		public CrashReportWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
