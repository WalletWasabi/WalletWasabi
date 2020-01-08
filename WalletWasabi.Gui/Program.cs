using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Threading;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using NBitcoin;
using Splat;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static StatusBarViewModel StatusBar = null;
		private static Global Global;
#pragma warning disable IDE1006 // Naming Styles

		private static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			bool runGui = false;
			try
			{
				Global = new Global();

				Locator.CurrentMutable.RegisterConstant(Global);

				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = await CommandInterpreter.ExecuteCommandsAsync(Global, args);
				
				if (!runGui)
				{
					return;
				}

				if (await Global.InitializeUiConfigAsync())
				{
					Logger.LogInfo($"{nameof(Global.UiConfig)} is successfully initialized.");
				}
				else
				{
					Logger.LogError("Failed to initialize UIConfig.");
					return;
				}
				
				Logger.LogSoftwareStarted("Wasabi GUI");

				BuildAvaloniaApp().StartShellApp("Wasabi Wallet", AppMainAsync, args);
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
				throw;
			}
			finally
			{
				StatusBar?.Dispose();
				await Global.DisposeAsync().ConfigureAwait(false);
				AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

				if (runGui)
				{
					Logger.LogSoftwareStopped("Wasabi GUI");
				}
			}
		}

		private static async void AppMainAsync(string[] args)
		{
			try
			{
				var uiConfigFilePath = Path.Combine(Global.DataDir, "UiConfig.json");
				var uiConfig = new UiConfig(uiConfigFilePath);
				await uiConfig.LoadOrCreateDefaultFileAsync();

				Global.InitializeUiConfig(uiConfig);
				Logger.LogInfo($"{nameof(Global.UiConfig)} is successfully initialized.");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}

			AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);
			MainWindowViewModel.Instance = new MainWindowViewModel();
			StatusBar = new StatusBarViewModel();
			MainWindowViewModel.Instance.StatusBar = StatusBar;

			await Global.InitializeNoWalletAsync();

			StatusBar.Initialize(Global.Nodes.ConnectedNodes, Global.Synchronizer);

			if (Global.Network != Network.Main)
			{
				MainWindowViewModel.Instance.Title += $" - {Global.Network}";
			}

			Dispatcher.UIThread.Post(GC.Collect);
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception);
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception);
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			bool useGpuLinux = true;

			var result = AppBuilder.Configure<App>();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result
					.UseWin32()
					.UseSkia();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				if (Helpers.Utils.DetectLLVMPipeRasterizer())
				{
					useGpuLinux = false;
				}

				result.UsePlatformDetect()
					.UseManagedSystemDialogs<AppBuilder, WasabiWindow>();
			}
			else
			{
				result.UsePlatformDetect();
			}

			return result
				.With(new Win32PlatformOptions { AllowEglInitialization = true, UseDeferredRendering = true })
				.With(new X11PlatformOptions { UseGpu = useGpuLinux, WmClass = "Wasabi Wallet" })
				.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = true })
				.With(new MacOSPlatformOptions { ShowInDock = true });
		}
	}
}
