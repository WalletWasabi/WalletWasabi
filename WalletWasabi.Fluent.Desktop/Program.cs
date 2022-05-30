using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.ReactiveUI;
using Splat;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.OpenGL;
using WalletWasabi.Fluent.CrashReport;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;
using LogLevel = WalletWasabi.Logging.LogLevel;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Fluent.Desktop;

public class Program
{
	private static Global? Global;

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static int Main(string[] args)
	{
		bool runGui = true;
		bool runGuiInBackground = args.Any(arg => arg.Contains(StartupHelper.SilentArgument));

		// Initialize the logger.
		string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
		SetupLogger(dataDir, args);

		Logger.LogDebug($"Wasabi was started with these argument(s): {(args.Any() ? string.Join(" ", args) : "none") }.");

		(UiConfig uiConfig, Config config) = LoadOrCreateConfigs(dataDir);
		using SingleInstanceChecker singleInstanceChecker = new(config.Network);

		try
		{
			singleInstanceChecker.EnsureSingleOrThrowAsync().GetAwaiter().GetResult();
		}
		catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
		{
			return 1;
		}
		catch (Exception ex)
		{
			Logger.LogCritical(ex);
			return 1;
		}

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		Exception? exceptionToReport = null;
		TerminateService terminateService = new(TerminateApplicationAsync, TerminateApplication);

		try
		{
			if (CrashReporter.TryGetExceptionFromCliArgs(args, out var exceptionToShow))
			{
				// Show the exception.
				BuildCrashReporterApp(exceptionToShow).StartWithClassicDesktopLifetime(args);
				runGui = false;
			}
		}
		catch (Exception ex)
		{
			// Anything happens here just log it and do not run the Gui.
			Logger.LogCritical(ex);
			runGui = false;
		}

		if (runGui)
		{
			try
			{
				Global = CreateGlobal(dataDir, uiConfig, config);

				Services.Initialize(Global);

				Logger.LogSoftwareStarted("Wasabi GUI");
				BuildAvaloniaApp(runGuiInBackground, terminateService)
					.AfterSetup(_ =>
					{
						var glInterface = AvaloniaLocator.CurrentMutable.GetService<IPlatformOpenGlInterface>();
						Logger.LogInfo(glInterface is { }
							? $"Renderer: {glInterface.PrimaryContext.GlInterface.Renderer}"
							: "Renderer: Avalonia Software");

						ThemeHelper.ApplyTheme(Global.UiConfig.DarkModeEnabled ? Theme.Dark : Theme.Light);
					})
					.StartWithClassicDesktopLifetime(args);
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogDebug(ex);
			}
			catch (Exception ex)
			{
				exceptionToReport = ex;
				Logger.LogCritical(ex);
			}
		}

		// Start termination/disposal of the application.
		terminateService.Terminate();

		if (exceptionToReport is { })
		{
			// Trigger the CrashReport process if required.
			CrashReporter.Invoke(exceptionToReport);
		}

		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		Logger.LogSoftwareStopped("Wasabi");

		return exceptionToReport is { } ? 1 : 0;
	}

	/// <summary>
	/// Initializes Wasabi Logger. Sets user-defined log-level, if provided.
	/// </summary>
	/// <example>Start Wasabi Wallet with <c>./wassabee --LogLevel=trace</c> to set <see cref="LogLevel.Trace"/>.</example>
	private static void SetupLogger(string dataDir, string[] args)
	{
		LogLevel? logLevel = null;

		foreach (string arg in args)
		{
			if (arg.StartsWith("--LogLevel="))
			{
				string value = arg.Split('=', count: 2)[1];

				if (Enum.TryParse(value, ignoreCase: true, out LogLevel parsedLevel))
				{
					logLevel = parsedLevel;
					break;
				}
			}
		}

		Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"), logLevel);
	}

	private static (UiConfig uiConfig, Config config) LoadOrCreateConfigs(string dataDir)
	{
		Directory.CreateDirectory(dataDir);

		UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
		uiConfig.LoadOrCreateDefaultFile();

		Config config = new(Path.Combine(dataDir, "Config.json"));
		config.LoadOrCreateDefaultFile();

		return (uiConfig, config);
	}

	private static Global CreateGlobal(string dataDir, UiConfig uiConfig, Config config)
	{
		var walletManager = new WalletManager(config.Network, dataDir, new WalletDirectories(config.Network, dataDir));

		return new Global(dataDir, config, uiConfig, walletManager);
	}

	/// <summary>
	/// Do not call this method it should only be called by TerminateService.
	/// </summary>
	private static async Task TerminateApplicationAsync()
	{
		Logger.LogSoftwareStopped("Wasabi GUI");

		if (Global is { } global)
		{
			await global.DisposeAsync().ConfigureAwait(false);
		}
	}

	private static void TerminateApplication()
	{
		MainViewModel.Instance.ClearStacks();
		MainViewModel.Instance.StatusIcon.Dispose();
	}

	private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Logger.LogDebug(e.Exception);
	}

	private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Required to bootstrap Avalonia's Visual Previewer")]
	private static AppBuilder BuildAvaloniaApp()
	{
		return BuildAvaloniaApp(false, null!);
	}

	// Avalonia configuration, don't remove
	private static AppBuilder BuildAvaloniaApp(bool startInBg, TerminateService terminateService)
	{
		bool useGpuLinux = true;

		var result = AppBuilder.Configure(() => new App(async () =>
			{
				if (Global is { } global && terminateService is not null)
				{
					await global.InitializeNoWalletAsync(terminateService);
				}
			}, startInBg))
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

	/// <summary>
	/// Sets up and initializes the crash reporting UI.
	/// </summary>
	/// <param name="serializableException"></param>
	/// <param name="logPath"></param>
	/// <returns></returns>
	private static AppBuilder BuildCrashReporterApp(SerializableException serializableException)
	{
		var result = AppBuilder
			.Configure(() => new CrashReportApp(serializableException))
			.UseReactiveUI();

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

		return result
			.With(new Win32PlatformOptions { AllowEglInitialization = false, UseDeferredRendering = true })
			.With(new X11PlatformOptions { UseGpu = false, WmClass = "Wasabi Wallet Crash Reporting" })
			.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = false })
			.With(new MacOSPlatformOptions { ShowInDock = true })
			.AfterSetup(_ => ThemeHelper.ApplyTheme(Theme.Dark));
	}
}
