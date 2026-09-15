using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Accepts either wallet-owned Send or a caller-owned fee presentation.</summary>
public sealed class MobileSendFeeSelector : UserControl
{
	private readonly StackPanel _root;
	private MobileSendFeeSelection? _owned;
	private object? _context;
	private bool _attached;

	public MobileSendFeeSelector()
	{
		AvaloniaXamlLoader.Load(this);
		_root = this.FindControl<StackPanel>("FeeRoot")!;
		_root.DataContext = null;
	}
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _attached = true; BindSource(); }
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; Release(); base.OnDetachedFromVisualTree(e); }
	protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); if (_attached) BindSource(); }
	private void BindSource()
	{
		if (ReferenceEquals(_context, DataContext)) return;
		Release();
		_context = DataContext;
		_root.DataContext = DataContext switch
		{
			SendViewModel source => _owned = source.CreateMobileFeeSelection(),
			MobileSendFeeSelection selection => selection,
			_ => null
		};
	}
	private void Release()
	{
		_root.DataContext = null;
		_owned?.Dispose();
		_owned = null;
		_context = null;
	}
}
