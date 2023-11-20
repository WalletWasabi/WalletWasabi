using Avalonia;
using Avalonia.Media;

namespace WalletWasabi.Fluent.iOS;

public static class AppBuilderIOSExtension
{
	public static AppBuilder SetupAppBuilder(AppBuilder appBuilder)
	{
		bool enableGpu = Services.PersistentConfig is null ? false : Services.PersistentConfig.EnableGpu;

		appBuilder.UseiOS();

		return appBuilder
			.WithInterFont()
			// TODO: This crashes the app!?
			//.With(new FontManagerOptions {DefaultFamilyName = "fonts:Inter#Inter, $Default"})
			.With(new SkiaOptions {MaxGpuResourceSizeBytes = 2560 * 1600 * 4 * 12});
		// TODO:
		// .With(new ???PlatformOptions
		// {
		// 	RenderingMode = enableGpu
		// 		? new[] { AndroidRenderingMode.Egl, AndroidRenderingMode.Software }
		// 		: new[] { AndroidRenderingMode.Software },
		// });
	}
}
