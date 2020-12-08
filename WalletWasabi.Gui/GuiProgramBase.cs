using AvalonStudio.Shell.Extensibility.Platforms;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.CrashReport;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Logging;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.ViewModels;
using Avalonia.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using System.Runtime.InteropServices;
using AvalonStudio.Shell;
using Avalonia.Dialogs;
using AvalonStudio.Extensibility;

namespace WalletWasabi.Gui
{
	public class GuiProgramBase
	{
		private Global Global { get; set; }

		// This is only needed to pass CrashReporter to AppMainAsync otherwise it could be a local variable in Main().
		private CrashReporter CrashReporter { get; } = new CrashReporter();

		private TerminateService TerminateService { get; }

		public GuiProgramBase()
		{
			TerminateService = new TerminateService(TerminateApplicationAsync);
		}

		public void Run(string[] args)
		{
			bool runGui = false;
			Exception? appException = null;

			try
			{
				Global = CreateGlobal();
				Locator.CurrentMutable.RegisterConstant(Global);
				Locator.CurrentMutable.RegisterConstant(CrashReporter);

				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = ProcessCliCommands(args);

				if (CrashReporter.IsReport)
				{
					StartCrashReporter(args);
				}
				else if (runGui)
				{
					Logger.LogSoftwareStarted("Wasabi GUI");
					BuildAvaloniaApp().StartShellApp("Wasabi Wallet", AppMainAsync, args);
				}
			}
			catch (Exception ex)
			{
				appException = ex;
			}

			TerminateAppAndHandleException(appException, runGui);
		}

		private Global CreateGlobal()
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

		private bool ProcessCliCommands(string[] args)
		{
			var daemon = new Daemon(Global, TerminateService);
			var interpreter = new CommandInterpreter(Console.Out, Console.Error);
			var executionTask = interpreter.ExecuteCommandsAsync(
				args,
				new MixerCommand(daemon),
				new PasswordFinderCommand(Global.WalletManager),
				new CrashReportCommand(CrashReporter));
			return executionTask.GetAwaiter().GetResult();
		}

		private void SetTheme() => AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);

		private async void AppMainAsync(string[] args)
		{
			try
			{
				SetTheme();
				var statusBarViewModel = new StatusBarViewModel(Global.DataDir, Global.Network, Global.Config, Global.HostedServices, Global.BitcoinStore.SmartHeaderChain, Global.Synchronizer, Global.LegalDocuments);
				MainWindowViewModel.Instance = new MainWindowViewModel(Global.Network, Global.UiConfig, Global.WalletManager, statusBarViewModel, IoC.Get<IShell>());

				await Global.InitializeNoWalletAsync(TerminateService);

				MainWindowViewModel.Instance.Initialize(Global.Nodes.ConnectedNodes);

				Dispatcher.UIThread.Post(GC.Collect);
			}
			catch (Exception ex)
			{
				// There is no other way to stop the creation of the WasabiWindow, we have to exit the application here instead of return to Main.
				TerminateAppAndHandleException(ex, true);
			}
		}

		/// <summary>
		/// This is a helper method until the creation of the window in AppMainAsync cannot be aborted without Environment.Exit().
		/// </summary>
		private void TerminateAppAndHandleException(Exception? ex, bool runGui)
		{
			if (ex is OperationCanceledException)
			{
				Logger.LogDebug(ex);
			}
			else if (ex is { })
			{
				Logger.LogCritical(ex);
				if (runGui)
				{
					CrashReporter.SetException(ex);
				}
			}

			TerminateService.Terminate(ex is { } ? 1 : 0);
		}

		/// <summary>
		/// Do not call this method it should only be called by TerminateService.
		/// </summary>
		private async Task TerminateApplicationAsync()
		{
			var mainViewModel = MainWindowViewModel.Instance;
			if (mainViewModel is { })
			{
				mainViewModel.Dispose();
			}

			if (CrashReporter.IsInvokeRequired is true)
			{
				// Trigger the CrashReport process.
				CrashReporter.TryInvokeCrashReport();
			}

			if (Global is { } global)
			{
				await global.DisposeAsync().ConfigureAwait(false);
			}

			AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
			TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

			if (mainViewModel is { })
			{
				Logger.LogSoftwareStopped("Wasabi GUI");
			}
		}

		private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception);
		}

		private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
		{
			Logger.LogWarning(e?.ExceptionObject as Exception);
		}

		private void StartCrashReporter(string[] args)
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

		private AppBuilder BuildAvaloniaApp()
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
