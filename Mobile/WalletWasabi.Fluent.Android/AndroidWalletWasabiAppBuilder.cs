using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;

namespace WalletWasabi.Fluent.Android;

public class AndroidWalletWasabiAppBuilder : IWalletWasabiAppBuilder
{
	public AppBuilder SetupAppBuilder(AppBuilder appBuilder)
	{
		bool enableGpu = Services.PersistentConfig is not null && Services.PersistentConfig.EnableGpu;

		appBuilder
			.UseReactiveUI()
			.UseSkia()
			.UseAndroid();

		return appBuilder
			.WithInterFont()
			.With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter, $Default" })
			.With(new SkiaOptions { MaxGpuResourceSizeBytes = 2560 * 1600 * 4 * 12 })
			.With(new AndroidPlatformOptions()
			{
				RenderingMode = enableGpu
					? new[] { AndroidRenderingMode.Egl, AndroidRenderingMode.Software }
					: new[] { AndroidRenderingMode.Software }
			});
	}
}
