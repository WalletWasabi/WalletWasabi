using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls;

public class ClipboardCopyButton : TemplatedControl
{
	public static readonly StyledProperty<ReactiveCommand<Unit, Unit>> CopyCommandProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, ReactiveCommand<Unit, Unit>>(nameof(CopyCommand));

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, string>(nameof(Text));

	public ClipboardCopyButton()
	{
		var canCopy = this.WhenAnyValue(x => x.Text, selector: text => text is not null);
		CopyCommand = ReactiveCommand.CreateFromTask(CopyToClipboardAsync, canCopy);
	}

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

	private async Task CopyToClipboardAsync()
	{
		await ApplicationHelper.SetTextAsync(Text);
		await Task.Delay(1000); // Introduces a delay while the animation is playing (1s). This will make the command 'busy' while being animated, avoiding reentrancy.
	}
}
