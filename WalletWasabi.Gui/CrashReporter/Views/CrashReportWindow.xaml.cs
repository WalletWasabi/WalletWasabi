using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Markup.Xaml;
using AvalonStudio.Shell.Controls;
using Splat;

namespace WalletWasabi.Gui.CrashReporter.Views
{
	public class CrashReportWindow : MetroWindow
	{
		public CrashReportWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
			Closing += CrashReportWindow_ClosingAsync;
		}

		private void CrashReportWindow_ClosingAsync(object sender, CancelEventArgs e)
		{
			Environment.Exit(0);
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
