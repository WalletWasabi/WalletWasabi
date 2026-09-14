using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Installed on MobileShell only. Unsupported routes fall through to the shared static locator.</summary>
public sealed class MobileViewLocator : IDataTemplate
{
	public bool Match(object? data) => data is WalletViewModel or SendViewModel or ReceiveViewModel or ReceiveAddressViewModel or TransactionPreviewViewModel or SendFeeViewModel or SettingsPageViewModel;
	public Control Build(object? data) => data switch
	{
		WalletViewModel => new MobileWalletView(),
		SendViewModel => new MobileSendView(),
		ReceiveViewModel => new MobileReceiveView(),
		ReceiveAddressViewModel => new MobileReceiveAddressView(),
		TransactionPreviewViewModel => new MobileTransactionPreviewView(),
		SendFeeViewModel => new MobileSendFeeView(),
		SettingsPageViewModel => new MobileSettingsView(),
		_ => throw new ArgumentException("Not a supported mobile route.", nameof(data))
	};
}

public sealed class MobileSendView : UserControl { public MobileSendView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileReceiveView : UserControl { public MobileReceiveView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileTransactionPreviewView : UserControl { public MobileTransactionPreviewView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileSettingsView : UserControl { public MobileSettingsView() => AvaloniaXamlLoader.Load(this); }

public sealed class MobileWalletView : UserControl
{
	private MobileWalletViewModel? _model;
	private bool _attached;
	public MobileWalletView() => AvaloniaXamlLoader.Load(this);
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindWallet(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindWallet(); }
	private void BindWallet()
	{
		if (_model?.Wallet == DataContext) return;
		Release();
		if (DataContext is WalletViewModel wallet)
		{
			_model = new MobileWalletViewModel(wallet);
			this.FindControl<Grid>("Root")!.DataContext = _model;
			_model.Activate();
		}
	}
	private void Release() { this.FindControl<Grid>("Root")!.DataContext = null; _model?.Dispose(); _model = null; }
}

public sealed class MobileReceiveAddressView : UserControl
{
	private MobileReceiveRequestViewModel? _model;
	private bool _attached;
	public MobileReceiveAddressView() => AvaloniaXamlLoader.Load(this);
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindAddress(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindAddress(); }
	private void BindAddress()
	{
		if (_model?.Source == DataContext) return;
		Release();
		if (DataContext is ReceiveAddressViewModel source) { _model = new MobileReceiveRequestViewModel(source); this.FindControl<MobilePage>("Root")!.DataContext = _model; }
	}
	private void Release() { this.FindControl<MobilePage>("Root")!.DataContext = null; _model?.Dispose(); _model = null; }
}

public sealed class MobileSendFeeView : UserControl
{
	private MobileFeeViewModel? _model;
	private bool _attached;
	public MobileSendFeeView() => AvaloniaXamlLoader.Load(this);
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindFee(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindFee(); }
	private void BindFee()
	{
		if (_model?.Source == DataContext) return;
		Release();
		if (DataContext is SendFeeViewModel source) { _model = new MobileFeeViewModel(source); this.FindControl<MobilePage>("Root")!.DataContext = _model; }
	}
	private void Release() { this.FindControl<MobilePage>("Root")!.DataContext = null; _model?.Dispose(); _model = null; }
}
