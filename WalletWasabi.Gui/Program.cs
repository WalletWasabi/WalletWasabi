using Avalonia;
using Avalonia.Threading;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using NBitcoin;
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
#pragma warning disable IDE1006 // Naming Styles

		private static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			StatusBarViewModel statusBar = null;
			bool runGui = true;
			try
			{
				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				//runGui = await CommandInterpreter.ExecuteCommandsAsync(args);
				if (!runGui)
				{
					return;
				}
				Logger.LogStarting("Wasabi GUI");

				BuildAvaloniaApp()
					.BeforeStarting(async builder =>
					{
						MainWindowViewModel.Instance = new MainWindowViewModel();

						await Global.InitializeNoWalletAsync();

						statusBar = new StatusBarViewModel(Global.Nodes.ConnectedNodes, Global.Synchronizer, Global.UpdateChecker);

						MainWindowViewModel.Instance.StatusBar = statusBar;

						if (Global.Network != Network.Main)
						{
							MainWindowViewModel.Instance.Title += $" - {Global.Network}";
						}

						Dispatcher.UIThread.Post(() =>
						{
							GC.Collect();
						});
					}).StartShellApp<AppBuilder, MainWindow>("Wasabi Wallet", null, () => MainWindowViewModel.Instance);
			}
			catch (Exception ex)
			{
				Logger.LogCritical<Program>(ex);
				throw;
			}
			finally
			{
				statusBar?.Dispose();
				await Global.DisposeAsync();
				AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

				if (runGui)
				{
					Logger.LogInfo($"Wasabi GUI stopped gracefully.", Logger.InstanceGuid.ToString());
				}
			}
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception, "UnobservedTaskException");
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception, "UnhandledException");
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			var result = AppBuilder.Configure<App>();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result
					.UseWin32()
					.UseDirect2D1();
			}
			else
			{
				result.UsePlatformDetect();
			}

			return result
				.With(new Win32PlatformOptions { AllowEglInitialization = true, UseDeferredRendering = true })
				.With(new X11PlatformOptions { UseGpu = true })
				.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = true })
				.With(new MacOSPlatformOptions { ShowInDock = true });
		}
	}
}
