using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class ClipboardCopyButton : TemplatedControl
{
	public static readonly StyledProperty<ReactiveCommand<Unit, Unit>> CopyCommandProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, ReactiveCommand<Unit, Unit>>(nameof(CopyCommand));

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, string>(nameof(Text));

	public ReactiveCommand<Unit, Unit> CopyCommand
	{
		get => GetValue(CopyCommandProperty);
		set => SetValue(CopyCommandProperty, value);
	}

	public string Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public ClipboardCopyButton()
	{
		var canCopy = this.WhenAnyValue(x => x.Text, selector: text => text is not null);
		CopyCommand = ReactiveCommand.CreateFromTask(CopyToClipboardAsync, canCopy);
	}

	private async Task CopyToClipboardAsync()
	{
		if (Application.Current is {Clipboard: { } clipboard})
		{
			await clipboard.SetTextAsync(Text);
		}
	}
}