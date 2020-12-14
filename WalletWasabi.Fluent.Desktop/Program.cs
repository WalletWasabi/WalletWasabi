using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Gui;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.CrashReport;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Desktop
{
	public class Program
	{
		private static Global? Global;

		// This is only needed to pass CrashReporter to AppMainAsync otherwise it could be a local variable in Main().
		private static readonly CrashReporter CrashReporter = new CrashReporter();

		private static readonly TerminateService TerminateService = new TerminateService(TerminateApplicationAsync);

		private static SingleInstanceChecker? SingleInstanceChecker { get; set; }

		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		public static void Main(string[] args)
		{
			int exitCode = 0;
			bool guiStarted = false;

			try
			{
				string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
				var (uiConfig, config) = LoadOrCreateConfigs(dataDir);

				SingleInstanceChecker = new SingleInstanceChecker(config.Network);
				Global = CreateGlobal(dataDir, uiConfig, config);

				// TODO only required due to statusbar vm... to be removed.
				Locator.CurrentMutable.RegisterConstant(Global);

				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				SingleInstanceChecker.EnsureSingleOrThrowAsync().GetAwaiter().GetResult();

				if (args.Length != 0)
				{
					ProcessCliCommands(Global, args);
				}
				else
				{
					if (CrashReporter.IsReport)
					{
						Console.WriteLine("TODO Implement crash reporting.");
						return;
					}

					Logger.LogSoftwareStarted("Wasabi GUI");
					guiStarted = true;
					BuildAvaloniaApp(Global)
						.AfterSetup(_ => ThemeHelper.ApplyTheme(Global.UiConfig.DarkModeEnabled))
						.StartWithClassicDesktopLifetime(args);
				}
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogDebug(ex);
			}
			catch (Exception ex)
			{
				exitCode = 1;
				Logger.LogCritical(ex);

				if (guiStarted)
				{
					CrashReporter.SetException(ex);
				}
			}

			TerminateService.Terminate();

			Environment.Exit(exitCode);
		}

		private static (UiConfig uiConfig, Config config) LoadOrCreateConfigs(string dataDir)
		{
			Directory.CreateDirectory(dataDir);

			UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
			uiConfig.LoadOrCreateDefaultFile();

			Config config = new(Path.Combine(dataDir, "Config.json"));
			config.LoadOrCreateDefaultFile();
			config.CorrectMixUntilAnonymitySet();

			return (uiConfig, config);
		}

		private static Global CreateGlobal(string dataDir, UiConfig uiConfig, Config config)
		{
			string torLogsFile = Path.Combine(dataDir, "TorLogs.txt");
			var walletManager = new WalletManager(config.Network, new WalletDirectories(dataDir));

			return new Global(dataDir, torLogsFile, config, uiConfig, walletManager);
		}

		private static bool ProcessCliCommands(Global global, string[] args)
		{
			var daemon = new Daemon(global, TerminateService);
			var interpreter = new CommandInterpreter(Console.Out, Console.Error);
			var executionTask = interpreter.ExecuteCommandsAsync(
				args,
				new MixerCommand(daemon),
				new PasswordFinderCommand(global.WalletManager),
				new CrashReportCommand(CrashReporter));
			return executionTask.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Do not call this method it should only be called by TerminateService.
		/// </summary>
		private static async Task TerminateApplicationAsync()
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

			if (SingleInstanceChecker is { } single)
			{
				await single.DisposeAsync().ConfigureAwait(false);
			}

			Logger.LogSoftwareStopped("Wasabi");
		}

		private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs? e)
		{
			if (e?.Exception != null)
			{
				Logger.LogWarning(e.Exception);
			}
		}

		private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs? e)
		{
			if (e?.ExceptionObject is Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		// Avalonia configuration, don't remove; also used by visual designer.
		private static AppBuilder BuildAvaloniaApp(Global global)
		{
			bool useGpuLinux = true;

			var result = AppBuilder.Configure(() => new App(global, async () => await global.InitializeNoWalletAsync(TerminateService)))
				.UseReactiveUI();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result
					.UseWin32()
					.UseSkia();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				result.UsePlatformDetect()
					.UseManagedSystemDialogs<AppBuilder, Window>();
			}
			else
			{
				result.UsePlatformDetect();
			}

			return result
				.With(new Win32PlatformOptions { AllowEglInitialization = true, UseDeferredRendering = true, UseWindowsUIComposition = true })
				.With(new X11PlatformOptions { UseGpu = useGpuLinux, WmClass = "Wasabi Wallet" })
				.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = true })
				.With(new MacOSPlatformOptions { ShowInDock = true });
		}
	}
}
