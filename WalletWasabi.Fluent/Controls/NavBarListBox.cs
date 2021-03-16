using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Styling;
using System;
using Avalonia;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Controls
{
	public class NavBarListBox : ListBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(ListBox);

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			this.GetObservable(SelectedItemProperty).Subscribe(OnSelectedItemPropertyChanged);
		}

		private void OnSelectedItemPropertyChanged(object? obj)
		{
			Console.WriteLine($"OnSelectedItemPropertyChanged {obj?.GetType().Name}");
		}

		protected override IItemContainerGenerator CreateItemContainerGenerator()
		{
			return new ItemContainerGenerator<NavBarItem>(
				this,
				ContentControl.ContentProperty,
				ContentControl.ContentTemplateProperty);
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			var previousSelectedItem = SelectedItem;
			// Console.WriteLine($"BEFORE OnPointerPressed {SelectedItem?.GetType().Name}");

			base.OnPointerPressed(e);

			// Console.WriteLine($"AFTER OnPointerPressed {SelectedItem?.GetType().Name}");

			var isSameSelectedItem = previousSelectedItem is not null && previousSelectedItem == SelectedItem;
			Console.WriteLine($"OnPointerPressed {isSameSelectedItem}");
			if (isSameSelectedItem)
			{
				Console.WriteLine($"RESELECT OnPointerPressed");
				SelectedItem = null;
				SelectedItem = previousSelectedItem;
			}
		}
	}
}
