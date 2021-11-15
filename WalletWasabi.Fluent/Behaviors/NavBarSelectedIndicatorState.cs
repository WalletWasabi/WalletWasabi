using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualExtensions = Avalonia.VisualExtensions;

namespace WalletWasabi.Fluent.Behaviors
{
	public class NavBarSelectedIndicatorState : IDisposable
	{
		private class DiscreteEasing : Easing
		{
			private readonly double _triggerPoint;

			public DiscreteEasing(double TriggerPoint)
			{
				_triggerPoint = TriggerPoint;
			}

			public override double Ease(double progress)
			{
				return (Math.Abs(progress - _triggerPoint) < double.Epsilon) ? 1 : 0;
			}
		}

		public readonly ConcurrentDictionary<int, Control> ScopeChildren = new();
		private readonly Easing bckEasing = new SplineEasing(0.2, 1, 0.1, 0.9);
		private readonly Easing fwdEasing = new SplineEasing(0.1, 0.9, 0.2);
		private readonly TimeSpan timebase = TimeSpan.FromSeconds(0.6);

		private bool _isDisposed;
		private bool _initialFixDone;
		private CancellationTokenSource _currentAnimationCts = new();

		/// <summary>
		/// The last animated indicator
		/// </summary>
		public Rectangle? PreviousIndicator { get; set; }

		// This will be used in the future for horizontal selection indicators.
		// ReSharper disable once UnusedMember.Global
		public Orientation NavItemsOrientation { get; set; } = Orientation.Vertical;

		public void Dispose()
		{
			_isDisposed = true;
			ScopeChildren.Clear();
		}

		public void AddChild(Control associatedObject)
		{
			if (ScopeChildren.ContainsKey(associatedObject.GetHashCode()))
			{
				return;
			}

			ScopeChildren.TryAdd(associatedObject.GetHashCode(),
				associatedObject);
		}

		private static Matrix GetOffsetFrom(IVisual ancestor, IVisual visual)
		{
			Matrix identity = Matrix.Identity;
			while (visual != ancestor)
			{
				var num = 0;
				var bounds = visual.Bounds;
				var topLeft = bounds.TopLeft;

				if (topLeft != new Point())
				{
					identity *= Matrix.CreateTranslation(topLeft);
				}

				visual = visual.VisualParent;

				if (visual == null)
				{
					throw new ArgumentException("'visual' is not a descendant of 'ancestor'.");
				}
			}

			return identity;
		}

		private async void AnimateIndicators(Rectangle previousIndicator, Rectangle nextIndicator,
			CancellationToken token)
		{
			if (_isDisposed || previousIndicator is null || nextIndicator is null)
			{
				return;
			}

			// Get the common ancestor as a reference point.
			var commonAncestor = previousIndicator.FindCommonVisualAncestor(nextIndicator);

			// Ignore the RenderTransforms so we can get the actual positions
			var prevMatrix = GetOffsetFrom(commonAncestor, previousIndicator);
			var nextMatrix = GetOffsetFrom(commonAncestor, nextIndicator);

			var prevVector = new Point().Transform(prevMatrix);
			var nextVector = new Point().Transform(nextMatrix);

			var targetVector = nextVector - prevVector;
			var fromTopToBottom = targetVector.Y > 0;
			var curEasing = fromTopToBottom ? fwdEasing : bckEasing;
			var newDim = Math.Abs(NavItemsOrientation == Orientation.Vertical ? targetVector.Y : targetVector.X);
			var maxScale = newDim / (NavItemsOrientation == Orientation.Vertical
				? nextIndicator.Bounds.Height
				: nextIndicator.Bounds.Width) + 1;


			nextIndicator.Opacity = 0;
			previousIndicator.Opacity = 1;

			var speedRatio = 1;

			Animation translationAnimation = new()
			{
				SpeedRatio = speedRatio,
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
							new Setter(TranslateTransform.XProperty, 0d),
							new Setter(TranslateTransform.YProperty, 0d)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(0.40d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, maxScale)
						}
					},
					new KeyFrame
					{
						Cue = new Cue(1d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, 1d),
							new Setter(TranslateTransform.XProperty, targetVector.X),
							new Setter(TranslateTransform.YProperty, targetVector.Y)
						}
					}
				}
			};

			await translationAnimation.RunAsync(previousIndicator, null, token);

			nextIndicator.Opacity = 1;
			previousIndicator.Opacity = 0;
		}

		public void InitialFix(Rectangle initial)
		{
			if (_initialFixDone)
			{
				return;
			}

			_initialFixDone = true;

			initial.Opacity = 1;
			PreviousIndicator = initial;
		}


		public void Animate(Rectangle NextIndicator)
		{
			// For Debouncing.
			if (PreviousIndicator == NextIndicator)
			{
				return;
			}

			_currentAnimationCts?.Cancel();
			_currentAnimationCts = new CancellationTokenSource();

			AnimateIndicators(PreviousIndicator, NextIndicator,
				_currentAnimationCts.Token);

			PreviousIndicator = NextIndicator;
		}
	}
}