using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Behaviors
{
	public class HideShowBehavior : DisposingBehavior<Window>
	{
		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			// On macOs the Hide/Show is a natural feature.
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				Observable
					.FromEventPattern(Services.SingleInstanceChecker, nameof(SingleInstanceChecker.OtherInstanceStarted))
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(_ =>
					{
						if (AssociatedObject.WindowState == WindowState.Minimized)
						{
							AssociatedObject.WindowState = (WindowState)Enum.Parse(typeof(WindowState), Services.UiConfig.WindowState);
						}

						// Fixes: https://github.com/zkSNACKs/WalletWasabi/issues/6309
						var temp = AssociatedObject.WindowState;
						AssociatedObject.Show();
						AssociatedObject.WindowState = temp;

						AssociatedObject.BringIntoView();
					})
					.DisposeWith(disposables);
			}

			// TODO: we need the close button click only, external close request should not be cancelled.
			Observable
				.FromEventPattern<CancelEventArgs>(AssociatedObject, nameof(AssociatedObject.Closing))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args =>
				{
					if (Services.UiConfig.HideOnClose)
					{
						if (AssociatedObject.WindowState is not WindowState.Minimized)
						{
							args.EventArgs.Cancel = true;
						}

						// AssociatedObject.Hide() and show Tray icon.
						// Temporary solution is to Minimize
						AssociatedObject.WindowState = WindowState.Minimized;
					}
				})
				.DisposeWith(disposables);
		}
	}
}
