using Avalonia.Headless.XUnit;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Fluent.Controls.AmountControlTests;

public class AmountControlTests
{
    [AvaloniaFact]
    public void AmountControl_Defaults()
    {
        var window = new AmountControl_Defaults();
        window.Show();

        Assert.Equal(window.DataContext, Amount.Zero);
    }
}
