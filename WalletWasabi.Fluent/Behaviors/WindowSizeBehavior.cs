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

		var isValidWidth = configWidth * currentScreen.PixelDensity <= currentScreen.WorkingArea.Width * currentScreen.PixelDensity;
		var isValidHeight = configHeight * currentScreen.PixelDensity <= currentScreen.WorkingArea.Height * currentScreen.PixelDensity;

		if (isValidWidth && isValidHeight)
		{
			window.Width = configWidth.Value;
			window.Height = configHeight.Value;

			var centerX = (int)((currentScreen.WorkingArea.Width - configWidth.Value) / 2);
			var centerY = (int)((currentScreen.WorkingArea.Height - configHeight.Value) / 2);
			window.Position = new PixelPoint(centerX, centerY);
		}
	}
}
