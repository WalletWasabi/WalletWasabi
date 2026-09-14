using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Mobile.Behaviors;

/// <summary>View-owned activity binding, including commands replaced after navigation.</summary>
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
				MobileCommandActivity.ObserveCommands(source.WhenAnyValue(x => x.IsBusy),
					source.WhenAnyValue(x => x.NextCommand), source.WhenAnyValue(x => x.SkipCommand))
					.ObserveOn(RxApp.MainThreadScheduler));
		});
		return new CompositeDisposable(context, activity);
	}
}
