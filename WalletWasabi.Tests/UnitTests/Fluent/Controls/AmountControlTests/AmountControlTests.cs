using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using VerifyXunit;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Fluent.Controls.AmountControlTests;

[UsesVerify]
public class AmountControlTests
{
    [AvaloniaFact]
    public Task AmountControl_001()
    {
        var window = new AmountControl001();
        window.Show();

        Assert.Equal(window.DataContext, Amount.Zero);

        return Verifier.Verify(window);
    }
}
