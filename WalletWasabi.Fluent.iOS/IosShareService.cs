using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CoreGraphics;
using Foundation;
using UIKit;
using WalletWasabi.Fluent.Mobile.Services;

namespace WalletWasabi.Fluent.IOS;

internal sealed class IosShareService : IMobileShareService
{
	private UIActivityViewController? _active;
	public async Task PresentAsync(string payload, CancellationToken cancellationToken)
	{
		Dispatcher.UIThread.VerifyAccess();
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentException.ThrowIfNullOrWhiteSpace(payload);
		if (_active is not null) throw new InvalidOperationException("A sharesheet is already open.");
		var window = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>()
			.Where(scene => scene.ActivationState == UISceneActivationState.ForegroundActive)
			.SelectMany(scene => scene.Windows).FirstOrDefault(candidate => candidate.IsKeyWindow);
		var presenter = window?.RootViewController ?? throw new InvalidOperationException("No foreground window can present the sharesheet.");
		while (presenter.PresentedViewController is { } next && !next.IsBeingDismissed) presenter = next;
		using var text = new NSString(payload);
		var controller = new UIActivityViewController(new NSObject[] { text }, null);
		_active = controller;
		controller.CompletionWithItemsHandler = (_, _, _, _) =>
		{
			if (ReferenceEquals(_active, controller)) _active = null;
			controller.Dispose();
		};
		try
		{
			if (controller.PopoverPresentationController is { } popover && presenter.View is { } view)
			{
				popover.SourceView = view;
				popover.SourceRect = new CGRect(view.Bounds.Width / 2, view.Bounds.Height / 2, 1, 1);
				popover.PermittedArrowDirections = (UIPopoverArrowDirection)0;
			}
			await presenter.PresentViewControllerAsync(controller, true);
		}
		catch
		{
			_active = null;
			controller.Dispose();
			throw;
		}
	}
}
