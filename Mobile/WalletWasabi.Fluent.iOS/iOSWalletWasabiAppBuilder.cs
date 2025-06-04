using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;

namespace WalletWasabi.Fluent.iOS;

// ReSharper disable once InconsistentNaming
public class iOSWalletWasabiAppBuilder : IWalletWasabiAppBuilder
{
	public AppBuilder SetupAppBuilder(AppBuilder appBuilder)
	{
		// TODO: On iOS, GPU rendering is always enabled - we need to hide that option from settings.
		bool enableGpu = Services.PersistentConfig is not null && Services.PersistentConfig.EnableGpu;

		appBuilder
			.UseReactiveUI()
			.UseSkia()
			.UseiOS();

		return appBuilder
			.WithInterFont()
			.With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter, $Default" })
			.With(new SkiaOptions { MaxGpuResourceSizeBytes = 2560 * 1600 * 4 * 12 })
			.With(new iOSPlatformOptions()
			{
				RenderingMode = [iOSRenderingMode.Metal, iOSRenderingMode.OpenGl]
			});
	}
}
