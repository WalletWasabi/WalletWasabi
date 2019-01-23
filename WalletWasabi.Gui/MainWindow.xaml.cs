using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Controls;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;

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
			Closing += MainWindow_ClosingAsync;
			AvaloniaXamlLoader.Load(this);
		}

		private async void MainWindow_ClosingAsync(object sender, CancelEventArgs e)
		{
			try
			{
				Global.UiConfig.WindowState = WindowState;
				Global.UiConfig.Width = Width;
				Global.UiConfig.Height = Height;
				await Global.UiConfig.ToFileAsync();
				Logging.Logger.LogInfo<UiConfig>("UiConfig is saved.");
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<MainWindow>(ex);
			}
		}

#pragma warning disable IDE1006 // Naming Styles

		private async void OnActivated(object sender, EventArgs e)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				Activated -= OnActivated;

				var uiConfigFilePath = Path.Combine(Global.DataDir, "UiConfig.json");
				var uiConfig = new UiConfig(uiConfigFilePath);
				await uiConfig.LoadOrCreateDefaultFileAsync();
				Global.InitializeUiConfig(uiConfig);
				Logging.Logger.LogInfo<UiConfig>("UiConfig is successfully initialized.");

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					MainWindowViewModel.Instance.Width = (double)uiConfig.Width;
					MainWindowViewModel.Instance.Height = (double)uiConfig.Height;
					MainWindowViewModel.Instance.WindowState = (WindowState)uiConfig.WindowState;
				}
				else
				{
					MainWindowViewModel.Instance.WindowState = WindowState.Maximized;
				}
				DisplayWalletManager();
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<MainWindow>(ex);
			}
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
