using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent.Controls
{
	public class TagControl : TemplatedControl
	{
		private IDisposable? _subscription;
		private TagsBox? _parentTagBox;

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_parentTagBox = Parent as TagsBox;

			_subscription?.Dispose();

			var deleteButton = e.NameScope.Find<Button>("PART_DeleteButton");

			deleteButton.Click += OnDeleteTagClicked;

			_subscription = Disposable.Create(() => deleteButton.Click -= OnDeleteTagClicked);
		}

		private void OnDeleteTagClicked(object? sender, RoutedEventArgs e)
		{
			_parentTagBox?.RemoveTargetTag(DataContext);
		}
	}
}