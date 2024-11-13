using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Helpers;

public static class PrivacyModeHelper
{
	private static readonly TimeSpan RevealDelay = TimeSpan.FromSeconds(0.25);
	private static readonly TimeSpan HideDelay = TimeSpan.FromSeconds(10);

	public static IObservable<bool> DelayedRevealAndHide(
		IObservable<bool> isPointerOver,
		IObservable<bool> isPrivacyModeEnabled,
		IObservable<bool>? isVisibilityForced = null)
	{
		isVisibilityForced ??= Observable.Return(false);

		var shouldBeVisible = isPointerOver
			.Select(Visibility)
			.Switch();

		IObservable<bool> finalVisibility = isPrivacyModeEnabled
			.CombineLatest(
				shouldBeVisible,
				isVisibilityForced,
				(privacyModeEnabled, visible, forced) => !privacyModeEnabled || visible || forced);

		return finalVisibility;
	}

	private static IObservable<bool> Visibility(bool isPointerOver)
	{
		if (isPointerOver)
		{
			return ShowAfterDelayThenHide();
		}

		return Hide();
	}

	private static IObservable<bool> Hide()
	{
		return Observable.Return(false);
	}

	private static IObservable<bool> ShowAfterDelayThenHide()
	{
		var hideObs = Observable
			.Return(false)
			.Delay(HideDelay);

		var showObs = Observable
			.Return(true)
			.Delay(RevealDelay);

		return showObs.Concat(hideObs);
	}
}
