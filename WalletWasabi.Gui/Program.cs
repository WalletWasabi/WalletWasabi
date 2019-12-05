using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Rendering;
using Avalonia.Threading;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using NBitcoin;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.Controls.LockScreen;
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
				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AvaloniaGlobalComponent.AvaloniaInstance = Global;
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = await CommandInterpreter.ExecuteCommandsAsync(Global, args);
				if (!runGui)
				{
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
				await Global.DisposeAsync();
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
			(Application.Current as App).SetDataContext(Global);
			AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);
			MainWindowViewModel.Instance = new MainWindowViewModel { Global = Global };
			StatusBar = new StatusBarViewModel(Global);
			MainWindowViewModel.Instance.StatusBar = StatusBar;
			MainWindowViewModel.Instance.LockScreen = new LockScreenViewModel(Global);

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
					.UseSkia()
					.UseManagedSystemDialogs<AppBuilder, WasabiWindow>();
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
