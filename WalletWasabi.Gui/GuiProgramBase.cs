using Splat;
using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Gui.CrashReport;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Logging;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui
{
	public class GuiProgramBase
	{
		public  Global? Global { get; private set; }

		// This is only needed to pass CrashReporter to AppMainAsync otherwise it could be a local variable in Main().
		private CrashReporter CrashReporter { get; } = new CrashReporter();

		public TerminateService TerminateService { get; }

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

				SetPlatformBaseDirectory(Global.DataDir);
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

				runGui = ProcessCliCommands(Global,args);

				if (CrashReporter.IsReport)
				{
					StartCrashReporter(args);
				}
				else if (runGui)
				{
					Logger.LogSoftwareStarted("Wasabi GUI");
					BuildAndRunAvaloniaApp(args);

				}
			}
			catch (Exception ex)
			{
				appException = ex;
			}

			TerminateAppAndHandleException(appException, runGui);
		}

		public virtual void BuildAndRunAvaloniaApp(string[] args)
		{
			throw new NotImplementedException();
		}

		public virtual void SetPlatformBaseDirectory(string datadir)
		{
			throw new NotImplementedException();
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

		private bool ProcessCliCommands(Global global, string[] args)
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

		public async void AppMainAsync(string[] _)
		{
			try
			{
				await InitAppMainAsync();
			}
			catch (Exception ex)
			{
				// There is no other way to stop the creation of the WasabiWindow, we have to exit the application here instead of return to Main.
				TerminateAppAndHandleException(ex, true);
			}
		}

		public virtual Task InitAppMainAsync()
		{
			throw new NotImplementedException();
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
			if (e.Exception is { } ex)
			{
				Logger.LogWarning(ex);
			}
		}

		private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public virtual void StartCrashReporter(string[] args)
		{
			throw new NotImplementedException();
		}

	}
}
