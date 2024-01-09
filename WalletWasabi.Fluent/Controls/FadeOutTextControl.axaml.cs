using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

[TemplatePart("PART_TrimmedTextBlock", typeof(TextBlock))]
[TemplatePart("PART_NoTrimTextBlock", typeof(FadeOutTextBlock))]
public class FadeOutTextControl : TemplatedControl
{
	public static readonly StyledProperty<string?> TextProperty =
		AvaloniaProperty.Register<FadeOutTextControl, string?>(nameof(Text));

	private TextBlock? _trimmedTextBlock;
	private FadeOutTextBlock? _noTrimTextBlock;

	public string? Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		_trimmedTextBlock = e.NameScope.Find<TextBlock>("PART_TrimmedTextBlock");
		_noTrimTextBlock = e.NameScope.Find<FadeOutTextBlock>("PART_NoTrimTextBlock");

		if (_trimmedTextBlock is not null && _noTrimTextBlock is not null)
		{
			_noTrimTextBlock.TrimmedTextBlock = _trimmedTextBlock;
		}
	}
}
