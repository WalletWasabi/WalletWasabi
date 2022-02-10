using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class ScrollingTextBlock : TextBlock
{
	/// <summary>
	/// Defines the <see cref="TextGap"/> property.
	/// </summary>
	public static readonly StyledProperty<double> TextGapProperty =
		AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(TextGap), 30d);

	/// <summary>
	/// Defines the <see cref="MarqueeSpeed"/> property.
	/// </summary>
	public static readonly StyledProperty<double> MarqueeSpeedProperty =
		AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(MarqueeSpeed), 1d);

	/// <summary>
	/// Defines the <see cref="DelayProperty"/> property.
	/// </summary>
	public static readonly StyledProperty<TimeSpan> DelayProperty =
		AvaloniaProperty.Register<ScrollingTextBlock, TimeSpan>(nameof(Delay), TimeSpan.FromSeconds(2));

	public ScrollingTextBlock()
	{
		this.WhenAnyValue(x => x.Text)
			.Subscribe(OnTextChanged);

		new Clock().Subscribe(Tick);

		TextWrapping = TextWrapping.NoWrap;

		AffectsRender<ScrollingTextBlock>(TextProperty);
	}

	private void OnTextChanged(string? obj)
	{
		if (obj is null || obj.Length >= _offset)
		{
			_offset = 0;
			_waiting = true;
			_waitCounter = TimeSpan.Zero;
			Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	/// <summary>
	/// Gets or sets the gap between animated text.
	/// </summary>
	public double TextGap
	{
		get => GetValue(TextGapProperty);
		set => SetValue(TextGapProperty, value);
	}

	/// <summary>
	/// Gets or sets the speed of text scrolling.
	/// </summary>
	public double MarqueeSpeed
	{
		get => GetValue(MarqueeSpeedProperty);
		set => SetValue(MarqueeSpeedProperty, value);
	}

	/// <summary>
	/// Gets or sets the delay between text animations.
	/// </summary>
	public TimeSpan Delay
	{
		get => GetValue(DelayProperty);
		set => SetValue(DelayProperty, value);
	}

	private bool _isConstrained;
	private TimeSpan _oldFrameTime;
	private TimeSpan _waitCounter;

	private bool _waiting;
	private bool _animate;
	private double _offset;

	private double _textWidth;
	private double _textHeight;
	private double[] _offsets = new double[3];

	private void Tick(TimeSpan curFrameTime)
	{
		var frameDelta = curFrameTime - _oldFrameTime;
		_oldFrameTime = curFrameTime;

		if (_waiting)
		{
			_waitCounter += frameDelta;

			if (_waitCounter >= Delay)
			{
				_waitCounter = TimeSpan.Zero;
				_waiting = false;
				Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
			}
		}
		else if (_animate)
		{
			_offset += MarqueeSpeed;

			if (_offset >= ((_textWidth + TextGap) * 2))
			{
				_offset = 0;
				_waiting = true;
			}

			;

			Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	public override void Render(DrawingContext context)
	{
		var background = Background;

		if (background != null)
		{
			context.FillRectangle(background, new Rect(Bounds.Size));
		}

		var padding = Padding;

		if (TextLayout != null)
		{
			_textWidth = TextLayout.Size.Width;
			_textHeight = TextLayout.Size.Height;

			var constraints = Bounds.Deflate(Padding);
			var constraintsWidth = constraints.Width;

			_isConstrained = _textWidth >= constraintsWidth;

			if (_isConstrained & !_waiting)
			{
				_animate = true;
				var tOffset = padding.Left - _offset;

				_offsets[0] = tOffset;
				_offsets[1] = tOffset + _textWidth + TextGap;
				_offsets[2] = tOffset + (_textWidth + TextGap) * 2;

				foreach (var offset in _offsets)
				{
					var nR = new Rect(offset, padding.Top, _textWidth, _textHeight);
					var nC = new Rect(0, padding.Top, constraintsWidth, constraints.Height);

					if (!nC.Intersects(nR))
					{
						continue;
					}

					using (context.PushSetTransform(Matrix.CreateTranslation(nR.Left, nR.Top)))
					{
						TextLayout.Draw(context);
					}
				}
			}
			else
			{
				_animate = false;

				using (context.PushSetTransform(Matrix.CreateTranslation(padding.Left, padding.Top)))
				{
					TextLayout.Draw(context);
				}
			}
		}
	}
}