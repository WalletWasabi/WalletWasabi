using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>
/// An explicitly revealed backup, with acknowledgement separate from verification.
/// Conceals itself when its content/context changes, an ancestor hides, or it detaches.
/// This is visual privacy, not operating-system screenshot protection or memory erasure.
/// </summary>
public sealed class MobileSecretPanel : HeaderedContentControl
{
	public static readonly StyledProperty<bool> IsRevealedProperty =
		AvaloniaProperty.Register<MobileSecretPanel, bool>(nameof(IsRevealed));
	public static readonly StyledProperty<bool> IsAcknowledgedProperty =
		AvaloniaProperty.Register<MobileSecretPanel, bool>(nameof(IsAcknowledged));
	private CompositeDisposable? _visibilitySubscriptions;

	public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }
	public bool IsAcknowledged { get => GetValue(IsAcknowledgedProperty); set => SetValue(IsAcknowledgedProperty, value); }

	public void Conceal()
	{
		SetCurrentValue(IsRevealedProperty, false);
		SetCurrentValue(IsAcknowledgedProperty, false);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == DataContextProperty || change.Property == ContentProperty ||
			(change.Property == IsVisibleProperty && !IsVisible)) Conceal();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		Conceal();
		_visibilitySubscriptions?.Dispose();
		_visibilitySubscriptions = new CompositeDisposable();
		foreach (var ancestor in this.GetVisualAncestors())
		{
			_visibilitySubscriptions.Add(ancestor.GetObservable(IsVisibleProperty).Subscribe(visible =>
			{
				if (!visible) Conceal();
			}));
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_visibilitySubscriptions?.Dispose();
		_visibilitySubscriptions = null;
		Conceal();
		base.OnDetachedFromVisualTree(e);
	}
}

/// <summary>Numbered display/confirmation token. Contains no word selection or wallet logic.</summary>
public sealed class MobileRecoveryWordTile : TemplatedControl
{
	public static readonly StyledProperty<int> IndexProperty =
		AvaloniaProperty.Register<MobileRecoveryWordTile, int>(nameof(Index));
	public static readonly StyledProperty<string?> WordProperty =
		AvaloniaProperty.Register<MobileRecoveryWordTile, string?>(nameof(Word));
	public static readonly StyledProperty<bool> IsConfirmedProperty =
		AvaloniaProperty.Register<MobileRecoveryWordTile, bool>(nameof(IsConfirmed));

	public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
	public string? Word { get => GetValue(WordProperty); set => SetValue(WordProperty, value); }
	public bool IsConfirmed { get => GetValue(IsConfirmedProperty); set => SetValue(IsConfirmedProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsConfirmedProperty) PseudoClasses.Set(":confirmed", IsConfirmed);
	}
}
