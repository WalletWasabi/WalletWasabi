using Avalonia;
using Avalonia.Dialogs;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using Splat;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.CrashReport;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static Global Global;

		/// Warning! In Avalonia applications Main must not be async. Otherwise application may not run on OSX.
		/// see https://github.com/AvaloniaUI/Avalonia/wiki/Unresolved-platform-support-issues
		private static void Main(string[] args)
		{
			bool runGui = false;
			try
			{
				Global = CreateGlobal();

				Locator.CurrentMutable.RegisterConstant(Global);

				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = ShouldRunGui(args);

				if (Global.CrashReporter.IsReport)
				{
					StartCrashReporter(args);
					return;
				}

				if (runGui)
				{
					Logger.LogSoftwareStarted("Wasabi GUI");

					BuildAvaloniaApp().StartShellApp("Wasabi Wallet", AppMainAsync, args);
				}
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
				Global.CrashReporter.SetException(ex);
				throw;
			}
			finally
			{
				DisposeAsync().GetAwaiter().GetResult();
			}
		}

		private static Global CreateGlobal()
		{
			string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			Directory.CreateDirectory(dataDir);
			string torLogsFile = Path.Combine(dataDir, "TorLogs.txt");

			var uiConfig = new UiConfig(Path.Combine(dataDir, "UiConfig.json"));
			uiConfig.LoadOrCreateDefaultFile();
			var config = new Config(Path.Combine(dataDir, "Config.json"));
			config.LoadOrCreateDefaultFile();
			config.CorrectMixUntilAnonymitySet();
			var walletManager = new WalletManager(config.Network, new WalletDirectories(dataDir));

			return new Global(dataDir, torLogsFile, config, uiConfig, walletManager);
		}

		private static bool ShouldRunGui(string[] args)
		{
			var daemon = new Daemon(Global);
			var interpreter = new CommandInterpreter(Console.Out, Console.Error);
			var executionTask = interpreter.ExecuteCommandsAsync(
				args,
				new MixerCommand(daemon),
				new PasswordFinderCommand(Global.WalletManager),
				new CrashReportCommand(Global.CrashReporter));
			return executionTask.GetAwaiter().GetResult();
		}

		private static void SetTheme() => AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);

		private static async void AppMainAsync(string[] args)
		{
			try
			{
				SetTheme();

				MainWindowViewModel.Instance = new MainWindowViewModel(Global.Network, Global.UiConfig, Global.WalletManager, new StatusBarViewModel(), IoC.Get<IShell>());

				await Global.InitializeNoWalletAsync();

				MainWindowViewModel.Instance.Initialize(Global.Nodes.ConnectedNodes, Global.Synchronizer);

				Dispatcher.UIThread.Post(GC.Collect);
			}
			catch (Exception ex)
			{
				if (!(ex is OperationCanceledException))
				{
					Logger.LogCritical(ex);
					Global.CrashReporter.SetException(ex);
				}

				await DisposeAsync();

				// There is no other way to stop the creation of the WasabiWindow.
				Environment.Exit(1);
			}
		}

		private static async Task DisposeAsync()
		{
			var disposeGui = MainWindowViewModel.Instance is { };
			if (disposeGui)
			{
				MainWindowViewModel.Instance.Dispose();
			}

			if (Global?.CrashReporter?.IsInvokeRequired is true)
			{
				// Trigger the CrashReport process.
				Global.CrashReporter.TryInvokeCrashReport();
			}

			if (Global is { } global)
			{
				await global.DisposeAsync().ConfigureAwait(false);
			}

			AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
			TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

			if (disposeGui)
			{
				Logger.LogSoftwareStopped("Wasabi GUI");
			}
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception);
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception);
		}

		private static void StartCrashReporter(string[] args)
		{
			var result = AppBuilder.Configure<CrashReportApp>().UseReactiveUI();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result
					.UseWin32()
					.UseSkia();
			}
			else
			{
				result.UsePlatformDetect();
			}

			result
				.With(new Win32PlatformOptions { AllowEglInitialization = false, UseDeferredRendering = true })
				.With(new X11PlatformOptions { UseGpu = false, WmClass = "Wasabi Wallet Crash Reporting" })
				.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = false })
				.With(new MacOSPlatformOptions { ShowInDock = true });

			result.StartShellApp("Wasabi Wallet", _ => SetTheme(), args);
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
