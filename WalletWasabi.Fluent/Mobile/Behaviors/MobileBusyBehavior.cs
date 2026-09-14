using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Mobile.Behaviors;

/// <summary>
/// Owns the busy binding only for recognized native route contexts. Detached pages and
/// replaced view models release all subscriptions; arbitrary design data is left alone.
/// </summary>
public sealed class MobileBusyBehavior : AttachedToVisualTreeBehavior<MobilePage>
{
	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		var page = AssociatedObject;
		if (page is null) return Disposable.Empty;
		var activity = new SerialDisposable();
		var context = page.GetObservable(StyledElement.DataContextProperty).Subscribe(data =>
		{
			activity.Disposable = Disposable.Empty;
			var source = data switch
			{
				RoutableViewModel route => route,
				MobileReceiveRequestViewModel request => request.Source,
				MobileFeeViewModel fees => fees.Source,
				_ => null
			};
			if (source is null) return;
			activity.Disposable = page.Bind(MobilePage.IsBusyProperty,
				MobileCommandActivity.Observe(source.WhenAnyValue(x => x.IsBusy), source.NextCommand, source.SkipCommand));
		});
		return new CompositeDisposable(context, activity);
	}
}
