using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.Desktop
{
	public class FluentProgram : ProgramBase
	{
		public override void SetPlatformBaseDirectory(string datadir)
		{
			// This does not need to be set in Fluent.
		}

		public override void StartCrashReporter(string[] args)
		{
			// TODO: insert crash report start code here.
		}

		public override void BuildAndRunAvaloniaApp(string[] args)
		{
			BuildAvaloniaApp()
				.AfterSetup(_ =>
				{
					ThemeHelper.ApplyTheme(Global!.UiConfig.DarkModeEnabled);
					AppMainAsync(args);
				})
				.StartWithClassicDesktopLifetime(args);
		}

		// Avalonia configuration, don't remove; also used by visual designer.
		private AppBuilder BuildAvaloniaApp()
		{
			bool useGpuLinux = true;

			var result = AppBuilder.Configure(() => new App(Global!))
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

		public override async Task InitAppMainAsync()
		{
			await Global!.InitializeNoWalletAsync(TerminateService);

			MainViewModel.Instance!.Initialize();

			Dispatcher.UIThread.Post(GC.Collect);
		}
	}
}
