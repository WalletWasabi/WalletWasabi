using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Controls;

public class CurrencyEntryBoxClipboardBehavior : AttachedToVisualTreeBehavior<CurrencyEntryBox>
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(0.2);

	private static readonly IObservable<long> PollingTimer =
		Observable.Timer(PollingInterval)
				  .Repeat();

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is not CurrencyEntryBox box)
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
