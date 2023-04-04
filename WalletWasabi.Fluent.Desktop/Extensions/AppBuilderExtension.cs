using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Platform;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Desktop.Extensions;

public static class AppBuilderExtension
{
	public static AppBuilder SetupAppBuilder(this AppBuilder appBuilder)
	{
		bool enableGpu = Services.Config is null ? false : Services.Config.EnableGpu;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			appBuilder
				.UseWin32()
				.UseSkia();
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			appBuilder.UsePlatformDetect()
				.UseManagedSystemDialogs<AppBuilder, Window>()
				.AfterPlatformServicesSetup(_ =>
					{
						var systemFontFamily = AvaloniaLocator.Current
							.GetRequiredService<IFontManagerImpl>()
							.GetDefaultFontFamilyName();

						if (string.IsNullOrEmpty(systemFontFamily))
						{
							Logger.LogWarning("A default system font family cannot be resolved. Using a fallback.");

							AvaloniaLocator.CurrentMutable
								.Bind<FontManagerOptions>()
								.ToConstant(new FontManagerOptions { DefaultFamilyName = "DejaVu Sans" });
						}
					});
		}
		else
		{
			appBuilder.UsePlatformDetect();
		}

		return appBuilder
			.With(new SkiaOptions { MaxGpuResourceSizeBytes = 2560 * 1600 * 4 * 12 })
			.With(new Win32PlatformOptions { AllowEglInitialization = enableGpu, UseDeferredRendering = true, UseWindowsUIComposition = true })
			.With(new X11PlatformOptions { UseGpu = enableGpu, WmClass = "Wasabi Wallet" })
			.With(new AvaloniaNativePlatformOptions { UseDeferredRendering = true, UseGpu = enableGpu })
			.With(new MacOSPlatformOptions { ShowInDock = true });
	}
}
