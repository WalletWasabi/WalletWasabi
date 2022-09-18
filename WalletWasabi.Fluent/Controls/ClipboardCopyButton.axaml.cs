using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class ClipboardCopyButton : TemplatedControl
{
	public static readonly StyledProperty<ReactiveCommand<Unit, Unit>> CopyCommandProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, ReactiveCommand<Unit, Unit>>(nameof(CopyCommand));

	public static readonly StyledProperty<bool> IsPopupOpenProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, bool>(nameof(IsPopupOpen));

	public static readonly StyledProperty<bool> IsPopupVisibleProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, bool>(nameof(IsPopupVisible), true);

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, string>(nameof(Text));

	public static readonly StyledProperty<string> CopiedMessageProperty =
		AvaloniaProperty.Register<ClipboardCopyButton, string>(nameof(Text), "Copied");

	public static readonly StyledProperty<Control> TargetProperty = AvaloniaProperty.Register<ClipboardCopyButton, Control>("Target");

	public Control Target
	{
		get => GetValue(TargetProperty);
		set => SetValue(TargetProperty, value);
	}

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

	public bool IsPopupVisible
	{
		get => GetValue(IsPopupVisibleProperty);
		set => SetValue(IsPopupVisibleProperty, value);
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
		var canCopy = this.WhenAnyValue(c => c.Text, selector: s => s is not null);
		CopyCommand = ReactiveCommand.CreateFromTask(CopyToClipboardAsync, canCopy);

		var isPopupOpen = CopyCommand
			.Select(_ =>
				Observable.Return(true).Concat(Observable.Timer(HidePopupTime).Select(l => false)))
			.Switch();

		this.Bind(IsPopupOpenProperty, isPopupOpen);

		var hasBeenJustCopied = Observable.Return(false)
			.Concat(CopyCommand
				.Select(_ => Observable.Return(true).Concat(Observable.Timer(TimeSpan.FromSeconds(1)).Select(_ => false)))
				.Switch());

		var isCopyButtonVisible = this
			.WhenAnyValue(x => x.Target.IsPointerOver, item => item.Text, (a, b) => a && !string.IsNullOrWhiteSpace(b))
			.CombineLatest(hasBeenJustCopied, (over, justCopied) => over || justCopied);

		this.Bind(IsVisibleProperty, isCopyButtonVisible);
	}

	public TimeSpan HidePopupTime { get; set; } = TimeSpan.FromSeconds(1);

	private async Task CopyToClipboardAsync()
	{
		if (Application.Current is {Clipboard: { } clipboard})
		{
			await clipboard.SetTextAsync(Text);
		}
	}
}
