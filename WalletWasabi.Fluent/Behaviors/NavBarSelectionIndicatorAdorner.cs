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

			newRect.Width = previousIndicator.Bounds.Width;
			newRect.Height = previousIndicator.Bounds.Height;
			newRect.Fill = previousIndicator.Fill;

			var timebase = TimeSpan.FromSeconds(1.2);
			var fromTopToBottom = prevVector.Y > nextVector.Y;

			var fwdEasing = new SplineEasing(0.1, 0.9, 0.2, 1);
			var bckEasing = new SplineEasing(0.1, 0.9, 0.2, 1);

			var curEasing = fromTopToBottom ? fwdEasing : bckEasing;

			RelativePoint initRO, endRO;

			double totalNewHeight, maxScale;

			if (fromTopToBottom)
			{
				initRO = RelativePoint.Parse("50%, 0%");
				endRO = RelativePoint.Parse("50%, 100%");

				totalNewHeight = Math.Abs(nextVector.Y - prevVector.Y);
			}
			else
			{
				initRO = RelativePoint.Parse("50%, 100%");
				endRO = RelativePoint.Parse("50%, 0%");

				totalNewHeight = Math.Abs(prevVector.Y - nextVector.Y);
			}

			// totalNewHeight += nextIndicator.Bounds.Height;

			maxScale = totalNewHeight / nextIndicator.Bounds.Height;

			var g = new Animation()
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
							// new Setter(RotateTransform.AngleProperty, fromTopToBottom ? 0 : 180),
							new Setter(RenderTransformOriginProperty, initRO),
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
							// new Setter(RotateTransform.AngleProperty, fromTopToBottom ? 0 : 180),
							new Setter(RenderTransformOriginProperty, endRO),
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
				await Task.WhenAll(z.RunAsync(newRect, null), g.RunAsync(newRect, null))
					.WithAwaitCancellationAsync(token);
			}
			catch (OperationCanceledException)
			{
			}

			SetLeft(newRect, nextVector.X);
			SetTop(newRect, nextVector.Y);
		}

		public void InitialFix(Rectangle initial)
		{
			var prevVector = initial.TranslatePoint(new Point(), this) ?? new Point();
			newRect.Width = initial.Bounds.Width;
			newRect.Height = initial.Bounds.Height;
			newRect.Fill = initial.Fill;
			SetLeft(newRect, prevVector.X);
			SetTop(newRect, prevVector.Y);
		}
	}
}