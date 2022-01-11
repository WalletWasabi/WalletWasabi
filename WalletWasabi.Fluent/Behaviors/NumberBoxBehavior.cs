using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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
			.Subscribe(async e =>
			{
				e.Handled = true;

				var text = await Application.Current.Clipboard.GetTextAsync();
				AssociatedObject.Text = CorrectInput(text);
			})
			.DisposeWith(disposables);
	}

	private string CorrectInput(string input)
	{
		return new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
	}
}
