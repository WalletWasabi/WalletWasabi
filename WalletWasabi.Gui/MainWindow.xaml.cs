using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Controls;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow
	{
		public MainWindow()
		{
			InitializeComponent();

			this.AttachDevTools();
	    }

		private void InitializeComponent()
		{
			Activated += OnActivated;
			AvaloniaXamlLoader.Load(this);
		}

		void OnActivated(object sender, EventArgs e)
		{
			Activated -= OnActivated;
			IoC.Get<IShell>().AddOrSelectDocument<WalletManagerViewModel>(new WalletManagerViewModel());
		}
	}
}
