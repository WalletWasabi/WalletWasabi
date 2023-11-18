using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Controls;

public static class CurrencyEntryBoxClipboardListener
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(0.2);

	private static readonly IObservable<long> PollingTimer =
		Observable.Timer(PollingInterval)
				  .Repeat();

	public static void Start(CurrencyEntryBox box)
	{
		Observable.FromEventPattern(box, nameof(CurrencyEntryBox.AttachedToVisualTree))
				  .Do(_ => OnAttachedToVisualTree(box))
				  .Take(1)
				  .Subscribe();
	}

	private static void OnAttachedToVisualTree(CurrencyEntryBox box)
	{
		if (TopLevel.GetTopLevel(box)?.Clipboard is not { } clipboard)
		{
			return;
		}

		PollingTimer
			.Select(_ => Observable.FromAsync(() => clipboard.GetTextAsync()))
			.Merge(1)
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(x => box.CurrencyFormat is { })
			.DistinctUntilChanged()
			.Do(x =>
			{
				// Validate that value can be parsed with current CurrencyFormat
				var value = box.CurrencyFormat.Parse(x ?? "");

				if (value is null)
				{
					box.ClipboardSuggestion = null;
					return;
				}

				var isValidValue = value > 0 && (box.MaxValue is null || box.MaxValue >= value);
				if (isValidValue)
				{
					box.ClipboardSuggestion = box.CurrencyFormat.Format(value.Value);
				}
				else
				{
					box.ClipboardSuggestion = null;
				}
			})
			.Subscribe();
	}
}
