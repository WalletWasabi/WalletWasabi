using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

// TODO: This is a workaround for this Avalonia issue (https://github.com/AvaloniaUI/Avalonia/issues/11850)
// Please, remove this Behavior and it's unique usage after it's been fixed.
public class RestartRendererBehavior : AttachedToVisualTreeBehavior<Window>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		this.WhenAnyValue(x => x.AssociatedObject!.WindowState)
			.Where(state => state == WindowState.Normal)
			.Where(_ => OperatingSystem.IsLinux())
			.Do(_ =>
			{
				AssociatedObject.Renderer.Start();
				AssociatedObject.Activate();
			})
			.Subscribe()
			.DisposeWith(disposable);
	}
}
