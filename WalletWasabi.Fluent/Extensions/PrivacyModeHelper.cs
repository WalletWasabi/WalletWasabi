using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class PrivacyModeHelper
{
	private static readonly TimeSpan RevealDelay = TimeSpan.FromSeconds(0.75);
	private static readonly TimeSpan HideDelay = TimeSpan.FromSeconds(10);

	public static IObservable<bool> DelayedRevealAndHide(
		IObservable<bool> pointerOver,
		IObservable<bool> isPrivacyModeEnabled,
		IObservable<bool>? isForced = null)
	{
		isForced ??= Observable.Return(false);
		var isPointerOver = pointerOver
			.Select(
				isTrue => isTrue
					? PointerOverObs()
					: PointerOutObs())
			.Switch();

		var displayContent = isPrivacyModeEnabled
			.CombineLatest(
				isPointerOver,
				isForced,
				(privacyModeEnabled, pointerOver, forced) => !privacyModeEnabled || pointerOver || forced);

		return displayContent;
	}

	private static IObservable<bool> PointerOutObs()
	{
		return Observable.Return(false);
	}

	private static IObservable<bool> PointerOverObs()
	{
		var hideObs =
			Observable
				.Return(false)
				.Delay(HideDelay);

		var showObs =
			Observable
				.Return(true)
				.Delay(RevealDelay);

		return showObs.Concat(hideObs);
	}
}
