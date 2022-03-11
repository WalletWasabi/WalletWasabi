using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels;
using System.Linq;

namespace WalletWasabi.Fluent.Behaviors;

public class MainWindowBindPositionBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable.FromEventPattern(AssociatedObject, nameof(Window.DataContextChanged))
				  .Subscribe(_ => BindPosition(disposables))
				  .DisposeWith(disposables);
	}

	private void BindPosition(CompositeDisposable disposables)
	{
		if (AssociatedObject!.DataContext is MainViewModel vm)
		{
			Observable.FromEventPattern(AssociatedObject, nameof(Window.PositionChanged))
				  .Subscribe(_ => vm.WindowPosition = AssociatedObject.Position)
				  .DisposeWith(disposables);

			vm.WhenAnyValue(x => x.WindowPosition)
			  .Subscribe(x => AssociatedObject.Position = x)
			  .DisposeWith(disposables);

			SetInitialPosition(vm);
		}
	}

	private void SetInitialPosition(MainViewModel vm)
	{
		if (vm.WindowState != WindowState.Normal)
		{
			return;
		}	

		var position = vm.WindowPosition;

		var isValidLeft =
			AssociatedObject!.Screens.All.Any(s => s.Bounds.X <= position.X);

		var isValidRight =
			AssociatedObject.Screens.All.Any(s => s.Bounds.Right >= position.X);

		var isValidTop =
			AssociatedObject!.Screens.All.Any(s => s.Bounds.TopLeft.Y <= position.Y);

		var isValidBottom =
			AssociatedObject.Screens.All.Any(s => s.Bounds.BottomRight.Y >= position.Y);

		var x =
			isValidLeft && isValidRight
			? position.X
			: (int)((AssociatedObject.Screens.Primary.Bounds.Width - vm.WindowWidth) / 2);

		var y =
			isValidTop && isValidBottom
			? position.Y
			: (int)((AssociatedObject.Screens.Primary.Bounds.Height - vm.WindowHeight) / 2);

		AssociatedObject.Position = new PixelPoint(x, y);

		var currentScreen = AssociatedObject.Screens.ScreenFromPoint(AssociatedObject.Position);

		var isValidWidth =
			vm.WindowWidth * currentScreen.PixelDensity <= currentScreen.WorkingArea.Width * currentScreen.PixelDensity;

		var isValidHeight =
			vm.WindowHeight * currentScreen.PixelDensity <= currentScreen.WorkingArea.Height;

		if (!isValidWidth || !isValidHeight)
		{
			vm.WindowState = WindowState.Maximized;
		}
	}
}
