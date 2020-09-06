using Avalonia;
using Avalonia.Markup.Xaml;
using AvalonStudio.Shell.Controls;
using Splat;
using System;
using System.ComponentModel;

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
