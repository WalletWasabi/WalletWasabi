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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;

// This is temporary and to facilitate the migration to new UI.
[assembly: InternalsVisibleTo("WalletWasabi.Fluent")]
[assembly: InternalsVisibleTo("WalletWasabi.Fluent.Desktop")]

namespace WalletWasabi.Gui
{
	public static class Program
	{
		private static Global Global;

		private static TerminateService TerminateService = new(TerminateApplicationAsync);

		/// Warning! In Avalonia applications Main must not be async. Otherwise application may not run on OSX.
		/// see https://github.com/AvaloniaUI/Avalonia/wiki/Unresolved-platform-support-issues
		private static void Main(string[] args)
		{
			Exception? appException = null;

			try
			{
				Global = CreateGlobal();
				Locator.CurrentMutable.RegisterConstant(Global);

				Platform.BaseDirectory = Path.Combine(Global.DataDir, "Gui");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				Logger.LogSoftwareStarted("Wasabi GUI");
				BuildAvaloniaApp().StartShellApp("Wasabi Wallet", AppMainAsync, args);
			}
			catch (Exception ex)
			{
				appException = ex;
			}

			TerminateAppAndHandleException(appException);
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
			var walletManager = new WalletManager(config.Network, dataDir, new WalletDirectories(config.Network, dataDir));

			Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"));

			return new Global(dataDir, torLogsFile, config, uiConfig, walletManager);
		}

		private static void SetTheme() => AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);

		private static async void AppMainAsync(string[] args)
		{
			try
			{
				SetTheme();
				var statusBarViewModel = new StatusBarViewModel(Global.DataDir, Global.Network, Global.Config, Global.HostedServices, Global.BitcoinStore.SmartHeaderChain, Global.Synchronizer);
				MainWindowViewModel.Instance = new MainWindowViewModel(Global.Network, Global.UiConfig, Global.WalletManager, statusBarViewModel, IoC.Get<IShell>());

				await Global.InitializeNoWalletAsync(TerminateService);

				MainWindowViewModel.Instance.Initialize(Global.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes);

				Dispatcher.UIThread.Post(GC.Collect);
			}
			catch (Exception ex)
			{
				// There is no other way to stop the creation of the WasabiWindow, we have to exit the application here instead of return to Main.
				TerminateAppAndHandleException(ex);
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// This is a helper method until the creation of the window in AppMainAsync cannot be aborted without Environment.Exit().
		/// </summary>
		private static void TerminateAppAndHandleException(Exception? ex)
		{
			if (ex is OperationCanceledException)
			{
				Logger.LogDebug(ex);
			}
			else if (ex is { })
			{
				Logger.LogCritical(ex);
			}

			TerminateService.Terminate();
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

		private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			Logger.LogWarning(e?.Exception);
		}

		private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
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
