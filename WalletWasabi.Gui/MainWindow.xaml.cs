using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Theme;
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
#if DEBUG
			this.AttachDevTools();
#endif

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				HasSystemDecorations = true;

				// This will need implementing properly once this is supported by avalonia itself.
				var color = (ColorTheme.CurrentTheme.Background as SolidColorBrush).Color;
				(PlatformImpl as Avalonia.Native.WindowImpl).SetTitleBarColor(color);
			}
		}

		private void InitializeComponent()
		{
			Activated += OnActivated;
			AvaloniaXamlLoader.Load(this);
		}

		private void OnActivated(object sender, EventArgs e)
		{
			Activated -= OnActivated;
			DisplayWalletManager();
		}

		private void DisplayWalletManager()
		{
			var isAnyWalletAvailable = Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any();

			var walletManagerViewModel = new WalletManagerViewModel();
			IoC.Get<IShell>().AddDocument(walletManagerViewModel);

			if (isAnyWalletAvailable)
			{
				walletManagerViewModel.SelectLoadWallet();
			}
			else
			{
				walletManagerViewModel.SelectGenerateWallet();
			}
		}
	}
}
