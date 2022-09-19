using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class WindowSizeBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		AssociatedObject?
			.WhenAnyValue(x => x.Bounds)
			.Where(b => !b.IsEmpty)
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

		var isValidWidth = configWidth <= currentScreen.WorkingArea.Width && configWidth >= window.MinWidth;
		var isValidHeight = configHeight <= currentScreen.WorkingArea.Height && configHeight >= window.MinHeight;

		if (isValidWidth && isValidHeight)
		{
			window.Width = configWidth.Value;
			window.Height = configHeight.Value;
		}
	}
}
