using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using VerifyXunit;

namespace WalletWasabi.Tests.UnitTests.Fluent.Controls.AnimatedButtonTests;

[UsesVerify]
public class AnimatedButtonTests
{
    [AvaloniaFact]
    public Task AnimatedButton_Defaults()
    {
        var window = new AnimatedButton_Defaults();
        window.Show();

        return Verifier.Verify(window);
    }
}
