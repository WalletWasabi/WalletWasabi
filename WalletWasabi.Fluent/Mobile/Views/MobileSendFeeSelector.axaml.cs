using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Views;

public sealed class MobileSendFeeSelector : UserControl
{
	private MobileSendFeeSelection? _selection;
	private SendViewModel? _source;
	private bool _attached;
	public MobileSendFeeSelector() => AvaloniaXamlLoader.Load(this);
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindSource(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindSource(); }
	private void BindSource()
	{
		if (ReferenceEquals(_source, DataContext)) return;
		Release();
		if (DataContext is not SendViewModel source) return;
		_source = source;
		_selection = source.CreateMobileFeeSelection();
		this.FindControl<StackPanel>("FeeRoot")!.DataContext = _selection;
	}
	private void Release()
	{
		this.FindControl<StackPanel>("FeeRoot")!.DataContext = null;
		_selection?.Dispose();
		_selection = null;
		_source = null;
	}
}
