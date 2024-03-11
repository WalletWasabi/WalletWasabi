using System.Numerics;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using WalletWasabi.Fluent.Controls.Rendering;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumControl : TemplatedControl
{
	public static readonly StyledProperty<bool> IsActiveProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsActive));

	public static readonly StyledProperty<bool> IsDockEffectVisibleProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsDockEffectVisible));

	private readonly SpectrumDrawHandler _spectrumDrawHandler;
	private CompositionCustomVisual? _customVisual;

	public SpectrumControl()
	{
		_spectrumDrawHandler = new SpectrumDrawHandler(this);

		Background = new RadialGradientBrush()
		{
			GradientStops =
			{
				new GradientStop { Color = Color.Parse("#00000D21"), Offset = 0 },
				new GradientStop { Color = Color.Parse("#FF000D21"), Offset = 1 }
			}
		};
	}

	public bool IsActive
	{
		get => GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	public bool IsDockEffectVisible
	{
		get => GetValue(IsDockEffectVisibleProperty);
		set => SetValue(IsDockEffectVisibleProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsActiveProperty)
		{
			_spectrumDrawHandler.OnIsActiveChanged();
		}
		else if (change.Property == IsDockEffectVisibleProperty)
		{
			if (change.GetNewValue<bool>() && !IsActive)
			{
				_spectrumDrawHandler.SplashEffectDataSource.Start();
			}
		}
		else if (change.Property == ForegroundProperty)
		{
			var foreground = Foreground ?? Brushes.Magenta;

			if (foreground is ImmutableSolidColorBrush brush)
			{
				_spectrumDrawHandler.OnForegroundChanged(brush.Color.ToSKColor());
			}
		}
	}

#if false
	public override void Render(DrawingContext context)
	{
		base.Render(context);

		_state.Render(context);
	}
#else
    protected override void OnLoaded(RoutedEventArgs routedEventArgs)
    {
        base.OnLoaded(routedEventArgs);

        var elemVisual = ElementComposition.GetElementVisual(this);
        var compositor = elemVisual?.Compositor;
        if (compositor is null)
        {
            return;
        }

        _customVisual = compositor.CreateCustomVisual(new DrawCompositionCustomVisualHandler());
        ElementComposition.SetElementChildVisual(this, _customVisual);

        LayoutUpdated += OnLayoutUpdated;

        _customVisual.Size = new Vector2((float)Bounds.Size.Width, (float)Bounds.Size.Height);
        _customVisual.SendHandlerMessage(new DrawPayload(HandlerCommand.Update, _spectrumDrawHandler));

        // TODO: Start();
    }

    protected override void OnUnloaded(RoutedEventArgs routedEventArgs)
    {
        base.OnUnloaded(routedEventArgs);

        LayoutUpdated -= OnLayoutUpdated;

        Stop();
        DisposeImpl();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_customVisual == null)
        {
            return;
        }

        _customVisual.Size = new Vector2((float)Bounds.Size.Width, (float)Bounds.Size.Height);
        _customVisual.SendHandlerMessage(new DrawPayload(HandlerCommand.Update, _spectrumDrawHandler, Bounds));
    }

    public void Start()
    {
        _customVisual?.SendHandlerMessage(new DrawPayload(HandlerCommand.Start, _spectrumDrawHandler, Bounds));
    }

    public void Stop()
    {
        _customVisual?.SendHandlerMessage(new DrawPayload(HandlerCommand.Stop));
    }

    private void DisposeImpl()
    {
        _customVisual?.SendHandlerMessage(new DrawPayload(HandlerCommand.Dispose));
    }
#endif
}
