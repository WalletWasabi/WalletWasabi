using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent.Controls
{
	public class TagControl : TemplatedControl
	{
		private TagsBox? _parentTagBox;

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			_parentTagBox = Parent as TagsBox;
			var deleteButton = e.NameScope.Find<Button>("PART_DeleteButton");
			deleteButton.Click += DeleteTag;
			base.OnApplyTemplate(e);
		}

		private void DeleteTag(object? sender, RoutedEventArgs e)
		{
			_parentTagBox?.RemoveTargetTag(DataContext);
		}
	}
}