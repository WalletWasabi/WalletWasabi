using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class NavBarSelectionIndicatorAdorner : Canvas, IDisposable
	{
		private CompositeDisposable disposables = new();
		private readonly Canvas _layer;
		private readonly Control _target;
		private readonly NavBarSelectedIndicatorState _sharedState;
		private bool _isDispose;
		private Rectangle newRect = new Rectangle();

		public NavBarSelectionIndicatorAdorner(Control element, NavBarSelectedIndicatorState sharedState)
		{
			_target = element;
			_sharedState = sharedState;
			_layer = AdornerLayer.GetAdornerLayer(_target);

			IsHitTestVisible = false;
			ClipToBounds = true;

			if (_layer is null)
			{
				return;
			}

			_sharedState.AdornerControl = this;

			_layer.Children.Add(this);
			Children.Add(newRect);

			newRect.VerticalAlignment = VerticalAlignment.Top;
			newRect.HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Top;
			HorizontalAlignment = HorizontalAlignment.Left;

			_target.WhenAnyValue(x => x.Bounds)
				.Subscribe(x =>
				{
					var prevVector = _target.TranslatePoint(new Point(), VisualRoot) ?? new Point();
					Width = x.Width;
					Height = x.Height;
					Margin = new Thickness(prevVector.X, prevVector.Y, 0, 0);
				})
				.DisposeWith(disposables);
		}

		private Rect bounds;

		public void Dispose()
		{
			_isDispose = true;
			_layer?.Children.Remove(this);
		}

		public async void AnimateIndicators(Rectangle previousIndicator, Rectangle nextIndicator,
			CancellationToken token,
			Orientation navItemsOrientation)
		{
			if (_isDispose)
			{
				return;
			}

			var prevVector = previousIndicator.TranslatePoint(new Point(), this) ?? new Point();
			var nextVector = nextIndicator.TranslatePoint(new Point(), this) ?? new Point();


			newRect.IsVisible = true;
			nextIndicator.Opacity = 0;
			previousIndicator.Opacity = 0;

			newRect.Width = previousIndicator.Bounds.Width;
			newRect.Height = previousIndicator.Bounds.Height;
			newRect.Fill = previousIndicator.Fill;

			var timebase = TimeSpan.FromSeconds(0.8);
			var fromTopToBottom = prevVector.Y > nextVector.Y;

			var fwdEasing = new SplineEasing(0.1, 0.9, 0.2, 1);
			var bckEasing = new SplineEasing(0.1, 0.9, 0.2, 1);

			var curEasing = fromTopToBottom ? fwdEasing : bckEasing;

			double newDim, maxScale;

			if (fromTopToBottom)
			{
				newDim = Math.Abs(nextVector.Y - prevVector.Y);
			}
			else
			{
				newDim = Math.Abs(prevVector.Y - nextVector.Y);
			}

			maxScale = newDim / nextIndicator.Bounds.Height;

			var g = new Animation()
			{
				Easing = curEasing,
				Duration = timebase ,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, 1d),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(0.33d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, maxScale),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, 1d),
						}
					}
				}
			};

			var z = new Animation()
			{
				Easing = curEasing,
				Duration = timebase,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0d),
						Setters =
						{
							new Setter(LeftProperty, prevVector.X),
							new Setter(TopProperty, prevVector.Y),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(LeftProperty, nextVector.X),
							new Setter(TopProperty, nextVector.Y),
						}
					}
				}
			};

			try
			{
				await Task.WhenAll(z.RunAsync(newRect, null, token), g.RunAsync(newRect, null, token));
			}
			catch (OperationCanceledException)
			{
			}

			newRect.IsVisible = false;
			nextIndicator.Opacity = 1;
			previousIndicator.Opacity = 0;

			SetLeft(newRect, nextVector.X);
			SetTop(newRect, nextVector.Y);
		}

		public void InitialFix(Rectangle initial)
		{
			var initialVector = initial.TranslatePoint(new Point(), this) ?? new Point();
			newRect.Width = initial.Bounds.Width;
			newRect.Height = initial.Bounds.Height;
			newRect.Fill = initial.Fill;
			SetLeft(newRect, initialVector.X);
			SetTop(newRect, initialVector.Y);
		}
	}
}