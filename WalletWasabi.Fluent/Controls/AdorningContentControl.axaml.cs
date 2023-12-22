using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls;

public enum RevealMode
{
	Manual,
	PointerOver
}

public class AdorningContentControl : ContentControl
{
	private IDisposable? _subscription;

	public static readonly StyledProperty<Control> AdornmentProperty =
		AvaloniaProperty.Register<AdorningContentControl, Control>(nameof(Adornment));

	public static readonly StyledProperty<RevealMode> RevealModeProperty =
		AvaloniaProperty.Register<AdorningContentControl, RevealMode>(nameof(RevealMode), RevealMode.PointerOver);

	public static readonly StyledProperty<bool> IsAdornmentVisibleProperty =
		AvaloniaProperty.Register<AdorningContentControl, bool>(nameof(IsAdornmentVisible), true);

	public Control Adornment
	{
		get => GetValue(AdornmentProperty);
		set => SetValue(AdornmentProperty, value);
	}

	public RevealMode RevealMode
	{
		get => GetValue(RevealModeProperty);
		set => SetValue(RevealModeProperty, value);
	}

	public bool IsAdornmentVisible
	{
		get => GetValue(IsAdornmentVisibleProperty);
		set => SetValue(IsAdornmentVisibleProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsPointerOverProperty)
		{
			Dispatcher.UIThread.Post(OnPointerOverChanged);
		}
		else if (change.Property == AdornmentProperty)
		{
			InvalidateAdornmentVisible(change.GetOldValue<Control>(), change.GetNewValue<Control>());
		}
		else if (change.Property == IsAdornmentVisibleProperty)
		{
			AdornerLayer.SetAdorner(this, change.GetNewValue<bool>() ? Adornment : null);
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);

		_subscription?.Dispose();

		InvalidateAdornmentVisible(null, null);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		InvalidateAdornmentVisible(null, Adornment);

		OnPointerOverChanged();
	}

	private void InvalidateAdornmentVisible(Control? oldValue, Control? newValue)
	{
		_subscription?.Dispose();

		if (oldValue is { })
		{
			AdornerLayer.SetAdorner(this, null);
		}

		if (newValue is { })
		{
			_subscription = newValue.GetObservable(IsPointerOverProperty)
				.Subscribe(_ => Dispatcher.UIThread.Post(OnPointerOverChanged));

			if (IsAdornmentVisible)
			{
				AdornerLayer.SetAdorner(this, Adornment);
			}
		}
	}

	private void OnPointerOverChanged()
	{
		if (RevealMode == RevealMode.PointerOver)
		{
			if (IsPointerOver)
			{
				IsAdornmentVisible = true;
			}
			else if (!Adornment.IsPointerOver)
			{
				IsAdornmentVisible = false;
			}
		}
	}
}
