using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class WhitespacePasteRemovalBehavior : DisposingBehavior<TextBox>
{
	protected override IDisposable OnAttachedOverride()
	{
		if (AssociatedObject is not { } tb)
		{
			return Disposable.Empty;
		}

		var pasteEvents = Observable.FromEventPattern<EventHandler<RoutedEventArgs>, RoutedEventArgs>(
			eh => tb.PastingFromClipboard += eh, eh => tb.PastingFromClipboard -= eh);

		return pasteEvents
			.Do(args => args.EventArgs.Handled =
				true) // Always mark the attempt to paste as handled, so we'll always use the customized paste.
			.Select(_ =>
				Observable.FromAsync(ApplicationHelper.GetTextAsync,
					scheduler: RxApp.MainThreadScheduler)) // Executes get text asynchronously using the UI thread
			.Concat() // Concatenates the results of the requests into a single observable
			.Do(clipboardText => Paste(clipboardText, tb)) // Pastes the text
			.Subscribe();
	}

	private static void Paste(string text, TextBox tb)
	{
		var pasted = text.WithoutWhitespace();
		var start = Math.Min(tb.SelectionStart,  tb.SelectionEnd);
		var end = Math.Max(tb.SelectionStart,  tb.SelectionEnd);

		var current = tb.Text ?? "";
		tb.Text = current[..start] + pasted + current[end..];
		tb.CaretIndex = start + pasted.Length;
	}
}
