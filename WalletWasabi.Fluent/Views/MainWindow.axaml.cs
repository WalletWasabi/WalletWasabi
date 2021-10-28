using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Views
{
	public class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
#if DEBUG
			this.AttachDevTools();
#endif
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			NotificationHelpers.ClearNotificationManager();

			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
			{
				desktopLifetime.MainWindow = null;

				Dispatcher.UIThread.Post(GC.Collect, DispatcherPriority.ContextIdle);
			}
		}
	}
}
