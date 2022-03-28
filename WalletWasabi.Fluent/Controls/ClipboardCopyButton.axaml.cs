using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class ClipboardCopyButton : TemplatedControl
{
	public static readonly StyledProperty<ReactiveCommand<Unit, Unit>> CopyCommandProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, ReactiveCommand<Unit, Unit>>(nameof(CopyCommand));

	public static readonly StyledProperty<bool> IsPopupOpenProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, bool>(nameof(IsPopupOpen));

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, string>(nameof(Text));

	public static readonly StyledProperty<string> CopiedMessageProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, string>(nameof(Text), "Copied");

	public ReactiveCommand<Unit, Unit> CopyCommand
	{
		get => GetValue(CopyCommandProperty);
		set => SetValue(CopyCommandProperty, value);
	}

	public bool IsPopupOpen
	{
		get => GetValue(IsPopupOpenProperty);
		set => SetValue(IsPopupOpenProperty, value);
	}

	public string Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public string CopiedMessage
	{
		get => GetValue(CopiedMessageProperty);
		set => SetValue(CopiedMessageProperty, value);
	}

	public ClipboardCopyButton()
	{
		CopyCommand = ReactiveCommand.CreateFromTask(CopyToClipboard);
		var obs = CopyCommand.Select(unit => true)
			.Merge(CopyCommand.Delay(TimeSpan.FromSeconds(2)).Select(_ => false));

		var o = CopyCommand
			.Select(unit =>
				Observable.Return(true).Concat(Observable.Timer(HidePopupTime).Select(l => false)))
			.Switch();

		this.Bind(IsPopupOpenProperty, o);
	}

	public TimeSpan HidePopupTime { get; set; } = TimeSpan.FromSeconds(2);

	private async Task CopyToClipboard()
	{
		if (Application.Current is {Clipboard: { } clipboard})
		{
			await clipboard.SetTextAsync(Text);
		}
	}
}