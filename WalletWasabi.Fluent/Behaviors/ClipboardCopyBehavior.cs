using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class ClipboardCopyBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<ClipboardCopyBehavior, object?>(nameof(Content));
	private readonly Flyout _flyoutBase;

	public ClipboardCopyBehavior()
	{
		_flyoutBase = new Flyout { Content = "Copied!" };
		CopyCommand = ReactiveCommand.CreateFromObservable(() => Copy);
	}

	private ReactiveCommand<Unit, Unit> CopyCommand { get; }

	public object? Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	private IObservable<Unit> Copy =>
		Observable
			.FromAsync(() => SetClipboardTextAsync(Content?.ToString()))
			.Concat(Observable.Timer(TimeSpan.FromSeconds(1)).ToSignal());

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Content ??= AssociatedObject;

		AssociatedObject.OnEvent(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel)
			.Where(x => x.EventArgs.GetCurrentPoint(AssociatedObject).Properties.IsRightButtonPressed)
			.ToSignal()
			.InvokeCommand(CopyCommand)
			.DisposeWith(disposable);

		CopyCommand.IsExecuting
			.Do(ToggleFlyout)
			.Subscribe()
			.DisposeWith(disposable);
	}

	private static async Task SetClipboardTextAsync(string? text)
	{
		if (text is null)
		{
			return;
		}

		if (Application.Current is { Clipboard: { } clipboard })
		{
			await clipboard.SetTextAsync(text);
		}
	}

	private void ToggleFlyout(bool isExecuting)
	{
		if (isExecuting && AssociatedObject is { })
		{
			_flyoutBase.ShowAt(AssociatedObject, true);
		}
		else
		{
			_flyoutBase.Hide();
		}
	}
}
