using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Mobile.Presentation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>
/// The production send surface. Its rendering contract is also usable by a component
/// explorer or headless tests without initializing wallet services. Supplied presentations
/// remain caller-owned; only an adapter created here is disposed here.
/// </summary>
public sealed class MobileSendSurface : UserControl
{
	private readonly StackPanel _root;
	private MobileSendPresentation? _ownedPresentation;
	private object? _boundContext;
	private bool _attached;

	public MobileSendSurface()
	{
		AvaloniaXamlLoader.Load(this);
		_root = this.FindControl<StackPanel>("SendRoot")!;
		_root.DataContext = null;
	}
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		_attached = true;
		BindPresentation();
	}
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_attached = false;
		Release();
		base.OnDetachedFromVisualTree(e);
	}
	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_attached) BindPresentation();
	}
	private void BindPresentation()
	{
		if (ReferenceEquals(_boundContext, DataContext)) return;
		Release();
		_boundContext = DataContext;
		if (DataContext is SendViewModel source)
			_root.DataContext = _ownedPresentation = new MobileSendPresentation(source);
		else if (DataContext is IMobileSendPresentation presentation)
			_root.DataContext = presentation;
	}
	private void Release()
	{
		_root.DataContext = null;
		_ownedPresentation?.Dispose();
		_ownedPresentation = null;
		_boundContext = null;
	}
}
