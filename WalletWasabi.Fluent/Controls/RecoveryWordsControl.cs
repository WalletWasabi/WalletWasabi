using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;

namespace WalletWasabi.Fluent.Controls;

public class RecoveryWordsControl : UserControl
{
	public static readonly StyledProperty<IEnumerable<RecoveryWordViewModel>> ItemsProperty =
		AvaloniaProperty.Register<RecoveryWordsControl, IEnumerable<RecoveryWordViewModel>>(nameof(Items));

	public static readonly StyledProperty<IDataTemplate> ItemTemplateProperty =
		AvaloniaProperty.Register<RecoveryWordsControl, IDataTemplate>(nameof(ItemTemplate));

	public RecoveryWordsControl()
	{
		this.WhenAnyValue(x => x.Items)
			.Subscribe(_ => OnItemsChanged());
	}

	public IEnumerable<RecoveryWordViewModel> Items
	{
		get => GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}

	public IDataTemplate ItemTemplate
	{
		get => GetValue(ItemTemplateProperty);
		set => SetValue(ItemTemplateProperty, value);
	}

	private void OnItemsChanged()
	{
		if (Items == null)
		{
			Content = null;
			return;
		}

		var items = Items.ToList();
		var itemsPerRow = items.Count / 2;

		var row1Items = items.Take(itemsPerRow).ToList();
		var row2Items = items.Except(row1Items).ToList();

		var row1 = new ItemsControl
		{
			ItemTemplate = ItemTemplate,
			Items = row1Items
		};

		var row2 = new ItemsControl
		{
			ItemTemplate = ItemTemplate,
			Items = row2Items
		};

		Content = new StackPanel
		{
			Children = { row1, row2 }
		};
	}
}
