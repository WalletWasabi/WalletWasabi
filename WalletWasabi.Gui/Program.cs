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
using WalletWasabi.Gui.Controls.LockScreen;
using WalletWasabi.Gui.ManagedDialogs;
using System.Threading;
using Avalonia.Rendering;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static Global Global;
#pragma warning disable IDE1006 // Naming Styles

		private static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			StatusBarViewModel statusBar = null;

			bool runGui = false;
			try
			{
				Global = new Global();
				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AvaloniaGlobalComponent.AvaloniaInstance = Global;
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = await CommandInterpreter.ExecuteCommandsAsync(Global, args);
				if (!runGui)
				{
					return;
				}
				Logger.LogStarting("Wasabi GUI");

				BuildAvaloniaApp()
					.BeforeStarting(async builder =>
					{
						MainWindowViewModel.Instance = new MainWindowViewModel { Global = Global };
						statusBar = new StatusBarViewModel(Global);
						MainWindowViewModel.Instance.StatusBar = statusBar;
						MainWindowViewModel.Instance.LockScreen = new LockScreenViewModel(Global);

						await Global.InitializeNoWalletAsync();

						statusBar.Initialize(Global.Nodes.ConnectedNodes, Global.Synchronizer, Global.UpdateChecker);

						if (Global.Network != Network.Main)
						{
							MainWindowViewModel.Instance.Title += $" - {Global.Network}";
						}
						Dispatcher.UIThread.Post(GC.Collect);
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
					.UseManagedSystemDialogs();
			}
			else
			{
				result.UsePlatformDetect()
					.UseManagedSystemDialogs();
			}

			// TODO remove this overriding of RenderTimer when Avalonia 0.9 is released.
			// fixes "Thread Leak" issue in 0.8.1 Avalonia.
			var old = result.WindowingSubsystemInitializer;

			result.UseWindowingSubsystem(() =>
			{
				old();

				AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new DefaultRenderTimer(60));
			});
			return result
				.With(new Win32PlatformOptions { AllowEglInitialization = true, UseDeferredRendering = true })
				.With(new X11PlatformOptions { UseGpu = useGpuLinux, WmClass = "Wasabi Wallet" })
				.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = true })
				.With(new MacOSPlatformOptions { ShowInDock = true });
		}
	}
}
