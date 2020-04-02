using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	public class BindWindowSizeBehavior : Behavior<Window>
	{
		private Size _maxSize;

		/// <summary>
		/// Defines the <see cref="Width"/> property.
		/// </summary>
		public static readonly StyledProperty<double> WidthProperty =
			AvaloniaProperty.Register<BindWindowSizeBehavior, double>(nameof(Width), double.NaN);

		/// <summary>
		/// Defines the <see cref="Height"/> property.
		/// </summary>
		public static readonly StyledProperty<double> HeightProperty =
			AvaloniaProperty.Register<BindWindowSizeBehavior, double>(nameof(Height), double.NaN);

		/// <summary>
		/// Gets or sets the width of the element.
		/// </summary>
		public double Width
		{
			get { return GetValue(WidthProperty); }
			set { SetValue(WidthProperty, value); }
		}

		/// <summary>
		/// Gets or sets the height of the element.
		/// </summary>
		public double Height
		{
			get { return GetValue(HeightProperty); }
			set { SetValue(HeightProperty, value); }
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			var screen = AssociatedObject.Screens.ScreenFromPoint(AssociatedObject.Position);

			_maxSize = screen.WorkingArea.Size.ToSize(screen.PixelDensity) * 0.95;

			this.GetObservable(HeightProperty)
				.Where(x => !double.IsNaN(x))
				.Subscribe(x =>
				{
					if (x >= _maxSize.Height)
					{
						AssociatedObject.Height = _maxSize.Height;
					}
				});

			this.GetObservable(WidthProperty)
				.Where(x => !double.IsNaN(x))
				.Subscribe(x =>
				{
					if (x >= _maxSize.Width)
					{
						AssociatedObject.Width = _maxSize.Width;
					}
				});
		}
	}
}
