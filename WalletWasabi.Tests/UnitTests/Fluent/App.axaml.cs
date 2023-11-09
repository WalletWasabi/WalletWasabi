using Avalonia.Markup.Xaml;
using Avalonia;

namespace WalletWasabi.Tests.UnitTests.Fluent;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
