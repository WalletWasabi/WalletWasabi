using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Behaviors;

public class PasteButtonFlashBehavior : AttachedToVisualTreeBehavior<AnimatedButton>
{
	private string? _lastFlashedOn;

	public static readonly StyledProperty<string> FlashAnimationProperty =
		AvaloniaProperty.Register<PasteButtonFlashBehavior, string>(nameof(FlashAnimation));

	public static readonly StyledProperty<string> CurrentAddressProperty =
		AvaloniaProperty.Register<PasteButtonFlashBehavior, string>(nameof(CurrentAddress));

	public string FlashAnimation
	{
		get => GetValue(FlashAnimationProperty);
		set => SetValue(FlashAnimationProperty, value);
	}

	public string CurrentAddress
	{
		get => GetValue(CurrentAddressProperty);
		set => SetValue(CurrentAddressProperty, value);
	}

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		RxApp.MainThreadScheduler.Schedule(async () => await CheckClipboardForValidAddressAsync());

		var disposables = new CompositeDisposable();

		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			var mainWindow = lifetime.MainWindow;

			Observable
				.FromEventPattern(mainWindow, nameof(mainWindow.Activated)).ToSignal()
				.Merge(this.WhenAnyValue(x => x.CurrentAddress).ToSignal())
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.SubscribeAsync(async _ => await CheckClipboardForValidAddressAsync(forceCheck: true))
				.DisposeWith(disposables);

			Observable
				.Interval(TimeSpan.FromMilliseconds(500))
				.ObserveOn(RxApp.MainThreadScheduler)
				.SubscribeAsync(async _ =>
				{
					if (!mainWindow.IsActive)
					{
						return;
					}

					await CheckClipboardForValidAddressAsync();
				})
				.DisposeWith(disposables);
		}

		AssociatedObject?.WhenAnyValue(x => x.AnimateIcon)
			.Where(x => x)
			.Subscribe(_ => AssociatedObject.Classes.Remove(FlashAnimation))
			.DisposeWith(disposables);

		return disposables;
	}

	private async Task CheckClipboardForValidAddressAsync(bool forceCheck = false)
	{
		var clipboardValue = await ApplicationHelper.GetTextAsync();

		// Yes, it can be null, the software crashed without this condition.
		if (clipboardValue is null)
		{
			return;
		}

		if (AssociatedObject is null)
		{
			return;
		}

		if (_lastFlashedOn == clipboardValue && !forceCheck)
		{
			return;
		}

		AssociatedObject.Classes.Remove(FlashAnimation);

		clipboardValue = clipboardValue.Trim();
		var addressParsingResult = AddressParser.Parse(clipboardValue, Services.WalletManager.Network);

		// ClipboardValue might not match CurrentAddress, but it might be a PayJoin address pointing to the CurrentAddress
		// Hence we need to compare both string value and parse result
		if (clipboardValue != CurrentAddress && addressParsingResult.IsOk && CurrentAddress != addressParsingResult.Value.ToWif(Services.WalletManager.Network))
		{
			AssociatedObject.Classes.Add(FlashAnimation);
			_lastFlashedOn = clipboardValue;
			ToolTip.SetTip(AssociatedObject, $"Paste BTC Address:\r\n{clipboardValue}");
		}
		else
		{
			ToolTip.SetTip(AssociatedObject, "Paste");
		}
	}
}
