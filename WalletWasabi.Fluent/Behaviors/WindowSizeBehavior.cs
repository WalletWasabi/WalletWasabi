using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
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
}
