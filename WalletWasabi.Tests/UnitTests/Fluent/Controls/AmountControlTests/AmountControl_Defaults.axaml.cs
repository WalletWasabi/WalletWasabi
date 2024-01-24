using Avalonia.Controls;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Tests.UnitTests.Fluent.Controls.AmountControlTests;

public partial class AmountControl_Defaults : Window
{
    public AmountControl_Defaults()
    {
        InitializeComponent();

        DataContext = Amount.Zero;
    }
}
