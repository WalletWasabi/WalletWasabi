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
using WalletWasabi.Fluent.ViewModels.Wallets.Settings;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Installed on MobileShell only. Unsupported routes fall through to the shared static locator.</summary>
public sealed class MobileViewLocator : IDataTemplate
{
	public bool Match(object? data) => data is WalletViewModel or SendViewModel or ReceiveViewModel or
		ReceiveAddressViewModel or TransactionPreviewViewModel or SendFeeViewModel or SettingsPageViewModel or
		WalletSettingsViewModel or WalletCoinJoinSettingsViewModel;
	public Control Build(object? data) => data switch
	{
		WalletViewModel => new MobileWalletView(),
		SendViewModel => new MobileSendView(),
		ReceiveViewModel => new MobileReceiveView(),
		ReceiveAddressViewModel => new MobileReceiveAddressView(),
		TransactionPreviewViewModel => new MobileTransactionPreviewView(),
		SendFeeViewModel => new MobileSendFeeView(),
		SettingsPageViewModel => new MobileSettingsView(),
		WalletSettingsViewModel => new MobileWalletSettingsView(),
		WalletCoinJoinSettingsViewModel => new MobileCoinJoinSettingsView(),
		_ => throw new ArgumentException("Not a supported mobile route.", nameof(data))
	};
}

public sealed class MobileSendView : UserControl { public MobileSendView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileReceiveView : UserControl { public MobileReceiveView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileTransactionPreviewView : UserControl { public MobileTransactionPreviewView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileSettingsView : UserControl { public MobileSettingsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileWalletSettingsView : UserControl { public MobileWalletSettingsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileCoinJoinSettingsView : UserControl { public MobileCoinJoinSettingsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileCoinJoinSettingsPanel : UserControl { public MobileCoinJoinSettingsPanel() => AvaloniaXamlLoader.Load(this); }

public sealed class MobileWalletView : UserControl
{
	private readonly Grid _root;
	private MobileWalletViewModel? _model;
	private MobileTransactionNavigation? _transactionNavigation;
	private bool _attached;

	public MobileWalletView()
	{
		AvaloniaXamlLoader.Load(this);
		_root = this.FindControl<Grid>("Root")!;
		// Its compiled bindings expect MobileWalletViewModel, not the parent WalletViewModel.
		_root.DataContext = null;
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindWallet(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindWallet(); }

	private void BindWallet()
	{
		if (ReferenceEquals(_model?.Wallet, DataContext)) return;
		Release();
		if (DataContext is WalletViewModel wallet)
		{
			_model = new MobileWalletViewModel(wallet);
			_root.DataContext = _model;
			try
			{
				_model.Activate();
				_transactionNavigation = MobileTransactionNavigation.Attach(_model);
			}
			catch
			{
				Release();
				throw;
			}
		}
	}

	private void Release()
	{
		_transactionNavigation?.Dispose();
		_transactionNavigation = null;
		_root.DataContext = null;
		_model?.Dispose();
		_model = null;
	}
}

public sealed class MobileReceiveAddressView : UserControl
{
	private readonly MobilePage _root;
	private MobileReceiveRequestViewModel? _model;
	private bool _attached;

	public MobileReceiveAddressView()
	{
		AvaloniaXamlLoader.Load(this);
		_root = this.FindControl<MobilePage>("Root")!;
		_root.DataContext = null;
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindAddress(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindAddress(); }

	private void BindAddress()
	{
		if (ReferenceEquals(_model?.Source, DataContext)) return;
		Release();
		if (DataContext is ReceiveAddressViewModel source)
		{
			_model = new MobileReceiveRequestViewModel(source);
			_root.DataContext = _model;
		}
	}

	private void Release() { _root.DataContext = null; _model?.Dispose(); _model = null; }
}

public sealed class MobileSendFeeView : UserControl
{
	private readonly MobilePage _root;
	private MobileFeeViewModel? _model;
	private bool _attached;

	public MobileSendFeeView()
	{
		AvaloniaXamlLoader.Load(this);
		_root = this.FindControl<MobilePage>("Root")!;
		_root.DataContext = null;
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindFee(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindFee(); }

	private void BindFee()
	{
		if (ReferenceEquals(_model?.Source, DataContext)) return;
		Release();
		if (DataContext is SendFeeViewModel source)
		{
			_model = new MobileFeeViewModel(source);
			_root.DataContext = _model;
		}
	}

	private void Release() { _root.DataContext = null; _model?.Dispose(); _model = null; }
}
