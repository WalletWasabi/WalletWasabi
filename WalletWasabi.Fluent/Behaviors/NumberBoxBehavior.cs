using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class NumberBoxBehavior : DisposingBehavior<TextBox>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject
			.AddDisposableHandler(InputElement.TextInputEvent, (_, e) =>
			{
				if (e.Text is { })
				{
					e.Text = CorrectInput(e.Text);
				}
			}, RoutingStrategies.Tunnel)
			.DisposeWith(disposables);

		Observable
			.FromEventPattern<RoutedEventArgs>(AssociatedObject, nameof(AssociatedObject.PastingFromClipboard))
			.Select(x => x.EventArgs)
			.SubscribeAsync(async e =>
			{
				e.Handled = true;

					if (Application.Current is { Clipboard: { } clipboard })
					{
						AssociatedObject.Text = CorrectInput(await clipboard.GetTextAsync());
					}
			})
			.DisposeWith(disposables);
	}

	private string CorrectInput(string input)
	{
		return new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
	}
}
