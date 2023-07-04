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
		bool enableGpu = Services.PersistentConfig is null ? false : Services.PersistentConfig.EnableGpu;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			appBuilder
				.UseWin32()
				.UseSkia();
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			appBuilder.UsePlatformDetect()
				.UseManagedSystemDialogs<Window>()
				.AfterPlatformServicesSetup(_ =>
					{
						/* TODO: Fix setting FontManagerOptions
						var systemFontFamily = AvaloniaLocator.Current
							.GetRequiredService<IFontManagerImpl>()
							.GetDefaultFontFamilyName();

						// No platform implementation can guarantee that the nullability contract won't be violated
						// by a native API calls. That's why Avalonia FontManager does exactly the same check.
						if (string.IsNullOrEmpty(systemFontFamily))
						{
							Logger.LogWarning("A default system font family cannot be resolved. Using a fallback.");

							AvaloniaLocator.CurrentMutable
								.Bind<FontManagerOptions>()
								.ToConstant(new FontManagerOptions { DefaultFamilyName = "Inter" });
						}*/
					});
		}
		else
		{
			appBuilder.UsePlatformDetect();
		}

		return appBuilder
			.With(new SkiaOptions { MaxGpuResourceSizeBytes = 2560 * 1600 * 4 * 12 })
			.With(new Win32PlatformOptions
			{
				RenderingMode = enableGpu
					? new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software }
					: new[] { Win32RenderingMode.Software },
				CompositionMode = new[] { Win32CompositionMode.WinUIComposition, Win32CompositionMode.RedirectionSurface }
			})
			.With(new X11PlatformOptions
			{
				RenderingMode = enableGpu
					? new[] { X11RenderingMode.Glx, X11RenderingMode.Software }
					: new[] { X11RenderingMode.Software },
				WmClass = "Wasabi Wallet"
			})
			.With(new AvaloniaNativePlatformOptions
			{
				RenderingMode = enableGpu
					? new[] { AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software }
					: new[] { AvaloniaNativeRenderingMode.Software },
			})
			.With(new MacOSPlatformOptions { ShowInDock = true });
	}
}
