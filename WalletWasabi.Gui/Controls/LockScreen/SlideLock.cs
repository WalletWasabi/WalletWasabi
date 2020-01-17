using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Utilities;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class SlideLock : ContentControl
	{
		private Thumb _thumb;
		private Grid _container;
		private double _value;
		private bool _isLocked;

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

		public static readonly DirectProperty<SlideLock, double> ValueProperty =
			RangeBase.ValueProperty.AddOwner<SlideLock>(o => o.Value, (o, v) => o.Value = v);

		public double Value
		{
			get { return _value; }
			set { SetAndRaise(ValueProperty, ref _value, value); }
		}

		public SlideLock()
		{
			Value = 100;

			this.GetObservable(ValueProperty)
				.Subscribe(x => Opacity = x / 100);
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

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			_thumb = e.NameScope.Find<Thumb>("PART_Thumb");
			_container = e.NameScope.Find<Grid>("PART_Container");

			_thumb.DragDelta += OnThumb_DragDelta;
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
