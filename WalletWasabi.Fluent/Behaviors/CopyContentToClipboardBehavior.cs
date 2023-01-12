using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class CopyContentToClipboardBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<CopyContentToClipboardBehavior, object?>(nameof(Content));
	public static readonly StyledProperty<DataTemplate> FlyoutMessageTemplateProperty = AvaloniaProperty.Register<CopyContentToClipboardBehavior, DataTemplate>(nameof(FlyoutMessageTemplate));
	public static readonly StyledProperty<object?> FlyoutMessageProperty = AvaloniaProperty.Register<CopyContentToClipboardBehavior, object?>(nameof(FlyoutMessage));
	private readonly Flyout _flyout;

	public CopyContentToClipboardBehavior()
	{
		_flyout = CreateFlyout();
		var canCopy = this.WhenAnyValue(x => x.Content).Select(x => x is not null);
		CopyToClipboardCommand = ReactiveCommand.CreateFromObservable(() => CopyToClipboard, canCopy);
	}

	public object? FlyoutMessage
	{
		get => GetValue(FlyoutMessageProperty);
		set => SetValue(FlyoutMessageProperty, value);
	}

	public DataTemplate FlyoutMessageTemplate
	{
		get => GetValue(FlyoutMessageTemplateProperty);
		set => SetValue(FlyoutMessageTemplateProperty, value);
	}

	private ReactiveCommand<Unit, Unit> CopyToClipboardCommand { get; }

	public object? Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	private IObservable<Unit> CopyToClipboard =>
		Observable
			.FromAsync(() => SetClipboardTextAsync(Content?.ToString()))
			.Concat(Observable.Timer(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler).ToSignal());

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject
			.OnEvent(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel)
			.Where(IsRightButtonPressed)
			.ToSignal()
			.InvokeCommand(CopyToClipboardCommand)
			.DisposeWith(disposable);

		CopyToClipboardCommand.IsExecuting
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

	private Flyout CreateFlyout()
	{
		return new Flyout
		{
			Content = new ContentPresenter
			{
				[!ContentPresenter.ContentTemplateProperty] = this[!FlyoutMessageTemplateProperty],
				[!ContentPresenter.ContentProperty] = this[!FlyoutMessageProperty]
			}
		};
	}

	private bool IsRightButtonPressed(EventPattern<PointerPressedEventArgs> eventPattern)
	{
		return eventPattern.EventArgs.GetCurrentPoint(AssociatedObject).Properties.IsRightButtonPressed;
	}

	private void ToggleFlyout(bool isVisible)
	{
		if (isVisible && AssociatedObject is { })
		{
			_flyout.ShowAt(AssociatedObject, true);
		}
		else
		{
			_flyout.Hide();
		}
	}
}
