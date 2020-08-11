using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Utilities;
using System;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLock : ContentControl
	{
		public static readonly DirectProperty<SlideLock, bool> CanSlideProperty =
			AvaloniaProperty.RegisterDirect<SlideLock, bool>(nameof(CanSlide), o => o.CanSlide, (o, v) => o.CanSlide = v);

		public static readonly DirectProperty<SlideLock, double> ThresholdProperty =
			AvaloniaProperty.RegisterDirect<SlideLock, double>(nameof(Threshold), o => o.Threshold, (o, v) => o.Threshold = v);

		public static readonly DirectProperty<SlideLock, bool> IsLockedProperty =
		AvaloniaProperty.RegisterDirect<SlideLock, bool>(nameof(IsLocked), o => o.IsLocked, (o, v) => o.IsLocked = v);

		public static readonly DirectProperty<SlideLock, bool> IsAnimationRunningProperty =
			AvaloniaProperty.RegisterDirect<SlideLock, bool>(nameof(IsAnimationRunning), o => o.IsAnimationRunning, (o, v) => o.IsAnimationRunning = v);

		public static readonly StyledProperty<double> ValueProperty =
			AvaloniaProperty.Register<SlideLock, double>(nameof(Value));

		private Thumb _thumb;
		private Panel _container;
		private bool _isLocked;
		private bool _isAnimationRunning;
		private Animation _closeAnimation;
		private Animation _openAnimation;
		private bool _canSlide;
		private double _threshold;

		static SlideLock()
		{
			AffectsArrange<SlideLock>(ValueProperty);
		}

		public SlideLock()
		{
			this.GetObservable(ValueProperty)
				.Subscribe(x => Opacity = x / 100);

			_closeAnimation = new Animation
			{
				Duration = TimeSpan.FromSeconds(1),
				Easing = new QuarticEaseInOut(),
				FillMode = FillMode.Both,
				Children =
				{
					new KeyFrame
					{
						Setters =
						{
							new Setter
							{
								Property = ValueProperty,
								Value = 100d
							}
						},
						Cue = new Cue(1d)
					}
				}
			};

			_openAnimation = new Animation
			{
				Duration = TimeSpan.FromSeconds(1),
				Easing = new QuarticEaseInOut(),
				FillMode = FillMode.Both,
				Children =
				{
					new KeyFrame
					{
						Setters =
						{
							new Setter
							{
								Property = ValueProperty,
								Value = 0d
							}
						},
						Cue = new Cue(1d)
					}
				}
			};

			this.GetObservable(IsLockedProperty)
				.Subscribe(async isLocked => await (isLocked ? RunCloseAnimationAsync() : RunOpenAnimationAsync()));
		}

		public bool CanSlide
		{
			get => _canSlide;
			set => SetAndRaise(CanSlideProperty, ref _canSlide, value);
		}

		public double Threshold
		{
			get => _threshold;
			set => SetAndRaise(ThresholdProperty, ref _threshold, value);
		}

		public bool IsLocked
		{
			get => _isLocked;
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public bool IsAnimationRunning
		{
			get => _isAnimationRunning;
			set => SetAndRaise(IsAnimationRunningProperty, ref _isAnimationRunning, value);
		}

		public double Value
		{
			get => GetValue(ValueProperty);
			set => SetValue(ValueProperty, value);
		}

		public async Task RunCloseAnimationAsync()
		{
			IsAnimationRunning = true;
			await _closeAnimation.RunAsync(this);
			IsAnimationRunning = false;
		}

		public async Task RunOpenAnimationAsync()
		{
			IsAnimationRunning = true;
			await _openAnimation.RunAsync(this);
			IsAnimationRunning = false;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var result = base.ArrangeOverride(finalSize);

			_container.Arrange(new Rect(0, ValueToDistance(Value) - Bounds.Height, finalSize.Width, finalSize.Height));

			return result;
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			_thumb = e.NameScope.Find<Thumb>("PART_Thumb");
			_container = e.NameScope.Find<Panel>("PART_Container");

			this.GetObservable(CanSlideProperty)
				.Subscribe(x => _thumb.IsHitTestVisible = x);

			_thumb.DragDelta += OnThumb_DragDelta;

			_thumb.DragCompleted += OnThumb_DragCompletedAsync;
		}

		private async void OnThumb_DragCompletedAsync(object sender, VectorEventArgs e)
		{
			if (CanSlide)
			{
				if (Value <= Threshold)
				{
					IsLocked = false;
					await RunOpenAnimationAsync();
				}
				else
				{
					await RunCloseAnimationAsync();
				}
			}
		}

		private double DistanceToValue(double distance)
		{
			return (distance * 100) / Bounds.Height;
		}

		private double ValueToDistance(double value)
		{
			return (Bounds.Height / 100) * value;
		}

		private void OnThumb_DragDelta(object sender, VectorEventArgs e)
		{
			if (CanSlide && Bounds.Height > 0)
			{
				var deltaValue = DistanceToValue(e.Vector.Y);

				Value = MathUtilities.Clamp(Value + deltaValue, 0, 100);
			}
		}
	}
}
