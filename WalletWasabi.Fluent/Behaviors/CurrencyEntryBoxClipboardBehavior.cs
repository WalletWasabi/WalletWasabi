using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Behaviors;

public class CurrencyEntryBoxClipboardBehavior : Avalonia.Xaml.Interactions.Custom.AttachedToVisualTreeBehavior<CurrencyEntryBox>
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(0.2);

	private static readonly IObservable<long> PollingTimer =
		Observable.Timer(PollingInterval)
				  .Repeat();

	public static readonly StyledProperty<decimal> MinValueProperty =
		AvaloniaProperty.Register<CurrencyEntryBoxClipboardBehavior, decimal>(nameof(MinValue));

	public decimal MinValue
	{
		get => GetValue(MinValueProperty);
		set => SetValue(MinValueProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is not { } box)
		{
			return;
		}

		if (TopLevel.GetTopLevel(box)?.Clipboard is not { } clipboard)
		{
			return;
		}

		PollingTimer
			.Select(_ => Observable.FromAsync(() => GetClipboardTextAsync(clipboard)))
			.Merge(1)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.DistinctUntilChanged()
			.Do(x =>
			{
				//				// Validate that value can be parsed with current CurrencyFormat
				//				var result = box.CurrencyFormat.Parse(x ?? "");
				//
				//				if (result is not CurrencyFormatParseResult.Ok ok)
				//				{
				//					box.ClipboardSuggestion = null;
				//					return;
				//				}
				//
				//				var value = ok.Value;
				//
				//				var isValidValue = value >= MinValue && (box.MaxValue is null || box.MaxValue >= value);
				//				if (isValidValue)
				//				{
				//					box.ClipboardSuggestion = box.CurrencyFormat.Format(value);
				//				}
				//				else
				//				{
				//					box.ClipboardSuggestion = null;
				//				}
			})
			.Subscribe()
			.DisposeWith(disposable);
	}

	private static async Task<string?> GetClipboardTextAsync(IClipboard clipboard)
	{
		try
		{
			return await clipboard.GetTextAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return null;
		}
	}
}
