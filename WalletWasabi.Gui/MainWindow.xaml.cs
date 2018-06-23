using System;
using System.IO;
using System.Linq;
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

		private void OnActivated(object sender, EventArgs e)
		{
			Activated -= OnActivated;
			if (Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any())
			{
				// Load
				var document = new WalletManagerViewModel();
				IoC.Get<IShell>().AddDocument(document);
				document.SelectedCategory = document.Categories.First(x => x is LoadWalletViewModel);
			}
			else
			{
				// Generate
				var document = new WalletManagerViewModel();
				IoC.Get<IShell>().AddDocument(document);
				document.SelectedCategory = document.Categories.First(x => x is GenerateWalletViewModel);
			}
		}
	}
}
