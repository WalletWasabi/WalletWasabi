using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace WalletWasabi.Fluent.Controls
{
	public class TagControl : ContentControl
	{
		private IDisposable? _subscription;
		private TagsBox? _parentTagBox;

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_parentTagBox = this.FindLogicalAncestorOfType<TagsBox>();

			_subscription?.Dispose();

			var deleteButton = e.NameScope.Find<Button>("PART_DeleteButton");

			if (deleteButton is { })
			{
				deleteButton.Click += OnDeleteTagClicked;

				_subscription = Disposable.Create(() => deleteButton.Click -= OnDeleteTagClicked);
			}
		}

		private void OnDeleteTagClicked(object? sender, RoutedEventArgs e)
		{

			_parentTagBox?.RemoveTargetTag(DataContext);
		}
	}
}