using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Native secondary flows. Every route retains the original view model and commands.</summary>
public sealed class MobileFlowViewLocator : IDataTemplate
{
    public bool Match(object? data) => data is CustomFeeRateDialogViewModel or ReceiveAddressesViewModel or
        AddressLabelEditViewModel or ConfirmHideAddressViewModel or SendSuccessViewModel or SuccessViewModel or TransactionDetailsViewModel;

    public Control Build(object? data) => data switch
    {
        CustomFeeRateDialogViewModel => new MobileCustomFeeView(),
        ReceiveAddressesViewModel => new MobileReceiveAddressesView(),
        AddressLabelEditViewModel => new MobileAddressLabelEditView(),
        ConfirmHideAddressViewModel => new MobileConfirmHideAddressView(),
        SendSuccessViewModel => new MobileSendSuccessView(),
        SuccessViewModel => new MobileSuccessView(),
        TransactionDetailsViewModel => new MobileTransactionDetailsView(),
        _ => throw new ArgumentException("Not a supported mobile flow.", nameof(data))
    };
}

public sealed class MobileCustomFeeView : UserControl { public MobileCustomFeeView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileReceiveAddressesView : UserControl { public MobileReceiveAddressesView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileAddressLabelEditView : UserControl { public MobileAddressLabelEditView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileConfirmHideAddressView : UserControl { public MobileConfirmHideAddressView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileSendSuccessView : UserControl { public MobileSendSuccessView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileSuccessView : UserControl { public MobileSuccessView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileTransactionDetailsView : UserControl { public MobileTransactionDetailsView() => AvaloniaXamlLoader.Load(this); }
