using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorState : IDisposable
{
	private readonly Easing _bckEasing = new SplineEasing(0.2, 1, 0.1, 0.9);
	private readonly Easing _fwdEasing = new SplineEasing(0.1, 0.9, 0.2);
	private readonly TimeSpan _totalDuration = TimeSpan.FromSeconds(0.4);

	private CancellationTokenSource _currentAnimationCts = new();
	private Control? _activeIndicator;

	private bool _isDisposed;
	private bool _previousAnimationOngoing;

	// This will be used in the future for horizontal selection indicators.
	public Orientation NavItemsOrientation { get; set; } = Orientation.Vertical;

	public void Dispose()
	{
		_isDisposed = true;
		CancelPriorAnimation();
	}

	private void CancelPriorAnimation()
	{
		_currentAnimationCts.Cancel();
		_currentAnimationCts.Dispose();
		_currentAnimationCts = new CancellationTokenSource();
	}

	private static Matrix GetOffsetFrom(IVisual ancestor, IVisual visual)
	{
		var identity = Matrix.Identity;
		while (visual != ancestor)
		{
			var bounds = visual.Bounds;
			var topLeft = bounds.TopLeft;

			if (topLeft != new Point())
			{
				identity *= Matrix.CreateTranslation(topLeft);
			}

			if (visual.VisualParent is null)
			{
				return Matrix.Identity;
			}

			visual = visual.VisualParent;
		}

		return identity;
	}

	public async void AnimateIndicatorAsync(Control next)
	{
		if (_isDisposed)
		{
			return;
		}

		var prevIndicator = _activeIndicator;
		var nextIndicator = next;

		// user clicked twice
		if (prevIndicator is null || prevIndicator.Equals(nextIndicator))
		{
			return;
		}

		// Get the common ancestor as a reference point.
		var commonAncestor = prevIndicator.FindCommonVisualAncestor(nextIndicator);

		// likely being dragged
		if (commonAncestor is null)
		{
			return;
		}

		_activeIndicator = next;

		if (_previousAnimationOngoing)
		{
			CancelPriorAnimation();
		}

		prevIndicator.Opacity = 1;
		nextIndicator.Opacity = 0;

		// Ignore the RenderTransforms so we can get the actual positions
		var prevMatrix = GetOffsetFrom(commonAncestor, prevIndicator);
		var nextMatrix = GetOffsetFrom(commonAncestor, nextIndicator);

		var prevVector = new Point().Transform(prevMatrix);
		var nextVector = new Point().Transform(nextMatrix);

		var targetVector = nextVector - prevVector;
		var fromTopToBottom = targetVector.Y > 0;
		var curEasing = fromTopToBottom ? _fwdEasing : _bckEasing;
		var newDim = Math.Abs(NavItemsOrientation == Orientation.Vertical ? targetVector.Y : targetVector.X);
		var maxScale = newDim / (NavItemsOrientation == Orientation.Vertical
			? nextIndicator.Bounds.Height
			: nextIndicator.Bounds.Width) + 1;

		Animation translationAnimation = new()
		{
			Easing = curEasing,
			Duration = _totalDuration,
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
						Cue = new Cue(0.33333d),
						Setters =
						{
							new Setter(ScaleTransform.ScaleYProperty, maxScale * 0.5d)
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

		_previousAnimationOngoing = true;
		await translationAnimation.RunAsync(prevIndicator, null, _currentAnimationCts.Token);
		_previousAnimationOngoing = false;

		prevIndicator.Opacity = 0;
		nextIndicator.Opacity = Equals(_activeIndicator, nextIndicator) ? 1 : 0;
	}

	public void SetActive(Control initial)
	{
		if (_activeIndicator is not null)
		{
			_activeIndicator.Opacity = 0;
		}

		_activeIndicator = initial;
		_activeIndicator.Opacity = 1;
	}
}
