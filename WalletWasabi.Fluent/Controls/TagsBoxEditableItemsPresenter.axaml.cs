using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public class TagsBoxEditableItemsPresenter : TagsBoxItemsPresenter
{
	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		ItemsControl = e.NameScope.Find<TagsBoxItemsControl>("PART_ItemsPresenter");
	}
}
