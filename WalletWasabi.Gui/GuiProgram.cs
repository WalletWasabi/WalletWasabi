using AvalonStudio.Shell.Extensibility.Platforms;
using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Gui.CrashReport;
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
	internal class GuiProgram : ProgramBase
	{
		public override void SetPlatformBaseDirectory(string datadir)
		{
			Platform.BaseDirectory = Path.Combine(datadir, "Gui");
		}

		public override void StartCrashReporter(string[] args)
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

		public override void BuildAndRunAvaloniaApp(string[] args)
		{
			BuildAvaloniaApp().StartShellApp("Wasabi Wallet", AppMainAsync, args);
		}

		public AppBuilder BuildAvaloniaApp()
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

		public override async Task InitAppMainAsync()
		{
			SetTheme();
			var statusBarViewModel = new StatusBarViewModel(Global.DataDir, Global.Network, Global.Config, Global.HostedServices, Global.BitcoinStore.SmartHeaderChain, Global.Synchronizer, Global.LegalDocuments);
			MainWindowViewModel.Instance = new MainWindowViewModel(Global.Network, Global.UiConfig, Global.WalletManager, statusBarViewModel, IoC.Get<IShell>());

			await Global.InitializeNoWalletAsync(TerminateService);

			MainWindowViewModel.Instance.Initialize(Global.Nodes.ConnectedNodes);

			Dispatcher.UIThread.Post(GC.Collect);
		}

		public void SetTheme() => AvalonStudio.Extensibility.Theme.ColorTheme.LoadTheme(AvalonStudio.Extensibility.Theme.ColorTheme.VisualStudioDark);
	}
}
