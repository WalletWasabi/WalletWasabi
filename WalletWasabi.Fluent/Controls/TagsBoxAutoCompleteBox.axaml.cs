using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class TagsBoxAutoCompleteBox : AutoCompleteBox
{
	internal TextBox? InternalTextBox;
	internal ListBox? SuggestionListBox;

	protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		InternalTextBox = e.NameScope.Find<TextBox>("PART_TextBox");
		SuggestionListBox = e.NameScope.Find<ListBox>("PART_SelectingItemsControl");
	}
}
