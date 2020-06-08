using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Threading;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using Splat;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.Container;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static Global Global;
		private static PureContainer PureContainer;

		/// Warning! In Avalonia applications Main must not be async. Otherwise application may not run on OSX.
		/// see https://github.com/AvaloniaUI/Avalonia/wiki/Unresolved-platform-support-issues
		private static void Main(string[] args)
		{
			bool runGui = false;
			try
			{
				using (BenchmarkLogger.Measure())
				{
					// <Temporary>
					string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
					Directory.CreateDirectory(dataDir);

					string torLogsFile = Path.Combine(dataDir, "TorLogs.txt");

					Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"));

					var uiConfig = new UiConfig(Path.Combine(dataDir, "UiConfig.json"));
					uiConfig.LoadOrCreateDefaultFile();

					var config = new Config(Path.Combine(dataDir, "Config.json"));
					config.LoadOrCreateDefaultFile();
					config.CorrectMixUntilAnonymitySet();

					var bitcoinStore = new BitcoinStore();
					var hostedServices = new HostedServices();
					var walletManager = new WalletManager(config.Network, new WalletDirectories(dataDir));
					var walletManagerLifecycle = new WalletManagerLifecycle(uiConfig, walletManager);
					var legalDocuments = LegalDocuments.TryLoadAgreed(dataDir);
					var statusBarViewModel = new StatusBarViewModel(dataDir, config, legalDocuments, hostedServices, bitcoinStore);

					Global = new Global(dataDir, torLogsFile, bitcoinStore, hostedServices, uiConfig, walletManager, walletManagerLifecycle, legalDocuments); // TODO: Remove
					var daemon = new Daemon(Global, walletManager); // TODO: Remove Globals

					PureContainer = new PureContainer(uiConfig, legalDocuments, walletManager, daemon, statusBarViewModel);

					walletManagerLifecycle.OnInit();

					Locator.CurrentMutable.RegisterConstant(Global); // TODO Remove
					Platform.BaseDirectory = Path.Combine(dataDir, "Gui");
				}
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
				
				runGui = PureContainer.GetSingletonCommandInterpreter().ExecuteCommandsAsync(args).GetAwaiter().GetResult();

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
				MainWindowViewModel.Instance?.Dispose();
				Global.DisposeAsync().GetAwaiter().GetResult();
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
				AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);
				MainWindowViewModel.Instance = PureContainer.GetSingletonMainWindowViewModel();
				MainWindowViewModel.Instance.InitStep1();

				await Global.InitializeNoWalletAsync();

				MainWindowViewModel.Instance.InitStep2(Global.Network, Global.Nodes, Global.Synchronizer);

				Dispatcher.UIThread.Post(GC.Collect);
			}
			catch (Exception ex)
			{
				if (!(ex is OperationCanceledException))
				{
					Logger.LogCritical(ex);
				}

				await Global.DisposeAsync().ConfigureAwait(false);
				Environment.Exit(1);
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
