using Avalonia.Controls;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Tests.UnitTests.Fluent.Controls.AmountControlTests;

public partial class AmountControl001 : Window
{
    public AmountControl001()
    {
        InitializeComponent();

        DataContext = Amount.Zero;
    }
}
