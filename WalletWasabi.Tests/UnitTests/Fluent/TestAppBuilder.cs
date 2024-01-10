using Avalonia;
using Avalonia.Headless;
using WalletWasabi.Tests.UnitTests.Fluent;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace WalletWasabi.Tests.UnitTests.Fluent;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
