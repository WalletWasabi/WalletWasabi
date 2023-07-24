using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

// TODO: This is a workaround for this Avalonia issue (https://github.com/AvaloniaUI/Avalonia/issues/11850)
// Please, remove this Behavior and it's unique usage after it's been fixed.
public class RestartRendererInLinuxBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject
			.WhenAnyValue(x => x.WindowState)
			.Where(state => state == WindowState.Normal)
			.Where(_ => OperatingSystem.IsLinux())
			.Do(_ =>
			{
				// TODO: IRenderer.Start() is PrivateApi and can't be accessed normally here.
				// (AssociatedObject as IRenderRoot).Renderer.Start();
				AssociatedObject.Activate();
			})
			.Subscribe()
			.DisposeWith(disposables);
	}
}
