using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Utilities;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLock : ContentControl
	{
		private Thumb _thumb;
		private Grid _container;
		private bool _isLocked;
		private Animation _closeAnimation;
		private Animation _openAnimation;

		// Threshold

		// Trigger animations to slide up and down		

		// enable or disable mouse control

		public static readonly DirectProperty<SlideLockScreenView, bool> IsLockedProperty =
		AvaloniaProperty.RegisterDirect<SlideLockScreenView, bool>(nameof(IsLocked), o => o.IsLocked, (o, v) => o.IsLocked = v);

		public bool IsLocked
		{
			get => _isLocked;
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public static readonly StyledProperty<double> ValueProperty =
			AvaloniaProperty.Register<SlideLock, double>(nameof(Value));

		public double Value
		{
			get => GetValue(ValueProperty);
			set => SetValue(ValueProperty, value);
		}

		public SlideLock()
		{
			this.GetObservable(ValueProperty)
				.Subscribe(x => Opacity = x / 100);

			_closeAnimation = new Animation
			{
				Duration = TimeSpan.FromSeconds(2.0),
				Easing = new BounceEaseOut(),
				FillMode = FillMode.Both,				
				Children =
				{
					new KeyFrame
					{
						Setters =
						{
							new Setter
							{
								Property = SlideLock.ValueProperty,
								Value = 0d
							}
						},
						Cue = new Cue(0d)
					},
					new KeyFrame
					{
						Setters =
						{
							new Setter
							{
								Property = SlideLock.ValueProperty,
								Value = 100d
							}
						},
						Cue = new Cue(1d)
					}
				}
			};

			_openAnimation = new Animation
			{
				Duration = TimeSpan.FromSeconds(2.0),
				Easing = new QuadraticEaseInOut(),
				FillMode = FillMode.Both,
				Children =
				{
					new KeyFrame
					{
						Setters =
						{
							new Setter
							{
								Property = SlideLock.ValueProperty,
								Value = 0d
							}
						},
						Cue = new Cue(1d)
					}
				}
			};
		}

		static SlideLock()
		{
			AffectsArrange<SlideLock>(ValueProperty);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var result = base.ArrangeOverride(finalSize);

			_container.Arrange(new Rect(0, -(Bounds.Height - ValueToDistance(Value)), finalSize.Width, finalSize.Height));

			return result;
		}

		protected override async void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			_thumb = e.NameScope.Find<Thumb>("PART_Thumb");
			_container = e.NameScope.Find<Grid>("PART_Container");

			_thumb.DragDelta += OnThumb_DragDelta;

			_thumb.DragCompleted += OnThumb_DragCompleted;

			await _closeAnimation.RunAsync(this);
		}

		private void OnThumb_DragCompleted(object sender, Avalonia.Input.VectorEventArgs e)
		{
			if(Value <= 75)
			{
				_openAnimation.RunAsync(this);
			}
		}

		private double DistanceToValue (double distance)
		{
			return (distance * 100) / Bounds.Height;
		}

		private double ValueToDistance (double value)
		{
			return (Bounds.Height / 100) * value;
		}

		private void OnThumb_DragDelta(object sender, Avalonia.Input.VectorEventArgs e)
		{
			var deltaValue = DistanceToValue(e.Vector.Y);

			Value = MathUtilities.Clamp(Value + deltaValue, 0, 100);
		}
	}
}
