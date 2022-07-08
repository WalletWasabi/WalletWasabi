using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
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
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.Opened))
			.Take(1)
			.Subscribe(_ =>
			{
				SetWindowSize(AssociatedObject);

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
			})
			.DisposeWith(disposables);
	}

	private void SetWindowSize(Window window)
	{
		var configWidth = Services.UiConfig.WindowWidth;
		var configHeight = Services.UiConfig.WindowHeight;
		var currentScreen = window.Screens.ScreenFromPoint(window.Position);

		if (configWidth is null || configHeight is null || currentScreen is null)
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
