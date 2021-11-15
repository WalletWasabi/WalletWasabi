using System;
using System.Linq;
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
using Avalonia.VisualTree;
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

		TimeSpan timebase = TimeSpan.FromSeconds(0.8);
		Easing fwdEasing = new SplineEasing(0.1, 0.9, 0.2, 1);
		Easing bckEasing = new SplineEasing(0.2, 1, 0.1, 0.9);

		public async void AnimateIndicators(Rectangle previousIndicator, Rectangle nextIndicator,
			CancellationToken token,
			Orientation navItemsOrientation)
		{
			if (_isDispose || previousIndicator is null || nextIndicator is null)
			{
				return;
			}

			previousIndicator.Opacity = 1;
			nextIndicator.Opacity = 0;

			var u = previousIndicator.GetVisualAncestors().OfType<StackPanel>().FirstOrDefault();

			var prevVector = previousIndicator.TranslatePoint(new Point(), u) ?? new Point();
			var nextVector = nextIndicator.TranslatePoint(new Point(), u) ?? new Point();

			var fromTopToBottom = prevVector.Y > nextVector.Y;

			var curEasing = fromTopToBottom ? fwdEasing : bckEasing;

			double direction = (fromTopToBottom ? -1d : 1d);
			double newDim, maxScale, targetY;

			newDim = Math.Abs(nextVector.Y - prevVector.Y);
			targetY = direction * newDim;

			maxScale = newDim / nextIndicator.Bounds.Height;

			Animation scalingAnimation = new()
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

			Animation translationAnimation = new()
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
							new Setter(TranslateTransform.XProperty, 0d),
							new Setter(TranslateTransform.YProperty, 0d),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(TranslateTransform.XProperty, 0d),
							new Setter(TranslateTransform.YProperty, targetY),
						}
					}
				}
			};


			Animation fadeOut = new()
			{
				Easing = new CubicEaseIn(),
				Duration = timebase / 4,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0.99d),
						Setters =
						{
							new Setter(OpacityProperty, 1d),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(OpacityProperty, 0d),
						}
					}
				}
			};


			Animation fadeIn = new()
			{
				Easing = new CubicEaseIn(),
				Duration = timebase,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0.99d),
						Setters =
						{
							new Setter(OpacityProperty, 0d),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(OpacityProperty, 1d),
						}
					}
				}
			};

			await Task.WhenAll(translationAnimation.RunAsync(previousIndicator, null, token),
				scalingAnimation.RunAsync(previousIndicator, null, token),
				fadeIn.RunAsync(nextIndicator, null, token),
				fadeOut.RunAsync(previousIndicator, null, token)
			);

			previousIndicator.Opacity = 0;
			nextIndicator.Opacity = 1;
		}


		public async void InitialFix(Rectangle initial, Orientation orientation)
		{
			Animation fadeIn = new()
			{
				FillMode = FillMode.Both,
				Easing = fwdEasing,
				Delay = TimeSpan.FromSeconds(0.150),
				Duration = timebase / 2,
				Children =
				{
					new KeyFrame
					{
						Cue = new Cue(0d),
						Setters =
						{
							new Setter(RenderTransformOriginProperty, RelativePoint.Parse("50%,100%")),
							new Setter(ScaleTransform.ScaleYProperty, 0d),
							new Setter(OpacityProperty, 0d),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(0.9999d),
						Setters =
						{
							new Setter(RenderTransformOriginProperty, RelativePoint.Parse("50%,100%")),
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(RenderTransformOriginProperty, RelativePoint.Center),
							new Setter(ScaleTransform.ScaleYProperty, 1d),
							new Setter(OpacityProperty, 1d),
						}
					}
				}
			};

			await fadeIn.RunAsync(initial, null);
		}
	}
}