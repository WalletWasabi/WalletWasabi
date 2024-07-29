using Avalonia.Headless.XUnit;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Fluent.Controls.AnimatedButtonTests;

public class AnimatedButtonTests
{
    //[AvaloniaFact]
    public void AnimatedButton_Defaults()
    {
        var window = new AnimatedButton_Defaults();
        window.Show();

        Assert.NotNull(window.TargetButton);
    }
}
