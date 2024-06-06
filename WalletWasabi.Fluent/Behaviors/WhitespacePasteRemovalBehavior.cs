using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class WhitespacePasteRemovalBehavior : DisposingBehavior<TextBox>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		var tb = AssociatedObject;

		if (tb != null)
		{
			var pasteEvents = Observable.FromEventPattern<EventHandler<RoutedEventArgs>, RoutedEventArgs>(
				eh => tb.PastingFromClipboard += eh, eh => tb.PastingFromClipboard -= eh);
			pasteEvents
				.Do(args => args.EventArgs.Handled = true)
				.Select(_ => Observable.FromAsync(ApplicationHelper.GetTextAsync, scheduler: RxApp.MainThreadScheduler))
				.Concat()
				.Do(clipboardText => Paste(clipboardText, tb))
				.Subscribe()
				.DisposeWith(disposables);
		}
	}

	private static void Paste(string text, TextBox tb)
	{
		var pasted = text.WithoutWhitespace();
		var start = tb.SelectionStart;
		var end = tb.SelectionEnd;

		var current = tb.Text ?? "";
		tb.Text = current[..start] + pasted + current[end..];
		tb.CaretIndex += pasted.Length;
	}
}
