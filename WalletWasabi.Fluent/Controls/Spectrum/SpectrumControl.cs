using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Skia;

namespace WalletWasabi.Fluent.Controls.Spectrum;

public class SpectrumControl : TemplatedControl
{
	public static readonly StyledProperty<bool> IsActiveProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsActive));

	public static readonly StyledProperty<bool> IsDockEffectVisibleProperty =
		AvaloniaProperty.Register<SpectrumControl, bool>(nameof(IsDockEffectVisible));

	private readonly SpectrumControlState _state;

	public SpectrumControl()
	{
		_state = new SpectrumControlState(this);

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
			_state.OnIsActiveChanged();
		}
		else if (change.Property == IsDockEffectVisibleProperty)
		{
			if (change.GetNewValue<bool>() && !IsActive)
			{
				_state._splashEffectDataSource.Start();
			}
		}
		else if (change.Property == ForegroundProperty)
		{
			_state._lineBrush = Foreground ?? Brushes.Magenta;

			if (_state._lineBrush is ImmutableSolidColorBrush brush)
			{
				_state._lineColor = brush.Color.ToSKColor();
			}
		}
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		_state.Render(context);
	}
}
