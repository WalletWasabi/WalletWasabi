using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class WindowSizeBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.Interval(TimeSpan.FromMilliseconds(50))
			.Select(_ => AssociatedObject.Screens.ScreenFromPoint(AssociatedObject.Position))
			.WhereNotNull()
			.Take(1)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(screen =>
			{
				SetWindowSize(AssociatedObject, screen);

				AssociatedObject
					.WhenAnyValue(x => x.Bounds)
					.Skip(1)
					.Where(b => !b.IsEmpty && AssociatedObject.WindowState == WindowState.Normal)
					.Subscribe(b =>
					{
						Services.UiConfig.WindowWidth = b.Width;
						Services.UiConfig.WindowHeight = b.Height;
					})
					.DisposeWith(disposables);
			}).DisposeWith(disposables);

		// Observable
		// 	.FromEventPattern(AssociatedObject, nameof(AssociatedObject.Opened))
		// 	.Take(1)
		// 	.Subscribe(_ =>
		// 	{
		// 		SetWindowSize(AssociatedObject);
		//
		// 		AssociatedObject
		// 			.WhenAnyValue(x => x.Bounds)
		// 			.Skip(1)
		// 			.Where(b => !b.IsEmpty && AssociatedObject.WindowState == WindowState.Normal)
		// 			.Subscribe(b =>
		// 			{
		// 				Services.UiConfig.WindowWidth = b.Width;
		// 				Services.UiConfig.WindowHeight = b.Height;
		// 			})
		// 			.DisposeWith(disposables);
		// 	})
		// 	.DisposeWith(disposables);
	}

	private void SetWindowSize(Window window, Screen currentScreen)
	{
		var configWidth = Services.UiConfig.WindowWidth;
		var configHeight = Services.UiConfig.WindowHeight;

		if (configWidth is null || configHeight is null)
		{
			return;
		}

		var scaledWidth = configWidth * currentScreen.PixelDensity;
		var scaledHeight = configHeight * currentScreen.PixelDensity;

		var isValidWidth = scaledWidth <= currentScreen.WorkingArea.Width && scaledWidth >= window.MinWidth;
		var isValidHeight = scaledHeight <= currentScreen.WorkingArea.Height && scaledHeight >= window.MinHeight;

		if (isValidWidth && isValidHeight)
		{
			window.Arrange(new Rect(0, 0, configWidth.Value, configHeight.Value));

			var centerX = (int)((currentScreen.WorkingArea.Width - scaledWidth) / 2);
			var centerY = (int)((currentScreen.WorkingArea.Height - scaledHeight) / 2);
			window.Position = new PixelPoint(centerX, centerY);
		}
	}
}
