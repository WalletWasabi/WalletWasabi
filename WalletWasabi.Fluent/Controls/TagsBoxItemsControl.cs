using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class TagsBoxItemsControl : ItemsControl, IStyleable
{
	private ICommand? _removeTagCommand;

	public static readonly StyledProperty<bool> EnableCounterProperty =
		AvaloniaProperty.Register<TagsBoxItemsControl, bool>(nameof(EnableCounter));

	public static readonly StyledProperty<bool> EnableDeleteProperty =
		AvaloniaProperty.Register<TagsBoxItemsControl, bool>(nameof(EnableDelete), true);

	public static readonly DirectProperty<TagsBoxItemsControl, ICommand?> RemoveTagCommandProperty =
		AvaloniaProperty.RegisterDirect<TagsBoxItemsControl, ICommand?>(
			nameof(RemoveTagCommand),
			o => o.RemoveTagCommand,
			(o, v) => o.RemoveTagCommand = v);

	public bool EnableCounter
	{
		get => GetValue(EnableCounterProperty);
		set => SetValue(EnableCounterProperty, value);
	}

	public bool EnableDelete
	{
		get => GetValue(EnableDeleteProperty);
		set => SetValue(EnableDeleteProperty, value);
	}

	public ICommand? RemoveTagCommand
	{
		get => _removeTagCommand;
		set => SetAndRaise(RemoveTagCommandProperty, ref _removeTagCommand, value);
	}

	Type IStyleable.StyleKey => typeof(ItemsControl);

	protected override IItemContainerGenerator CreateItemContainerGenerator()
	{
		var generator = new ItemContainerGenerator<TagControl>(
			this,
			ContentControl.ContentProperty,
			ContentControl.ContentTemplateProperty);

		generator.Materialized += (_, args) =>
		{
			foreach (var container in args.Containers)
			{
				if (container.ContainerControl is TagControl tagControl)
				{
					tagControl.OrdinalIndex = container.Index + 1;
					tagControl.EnableCounter = EnableCounter;
					tagControl.EnableDelete = EnableDelete;
					tagControl.RemoveTagCommand = RemoveTagCommand;
				}
			}
		};

		generator.Dematerialized += (_, args) =>
		{
			foreach (var container in args.Containers)
			{
				if (container.ContainerControl is TagControl tagControl)
				{
					tagControl.OrdinalIndex = -1;
					tagControl.EnableCounter = false;
					tagControl.EnableDelete = false;
					tagControl.RemoveTagCommand = null;
				}
			}
		};

		generator.Recycled += (_, args) =>
		{
			foreach (var container in args.Containers)
			{
				if (container.ContainerControl is TagControl tagControl)
				{
					tagControl.OrdinalIndex = container.Index + 1;
					tagControl.EnableCounter = EnableCounter;
					tagControl.EnableDelete = EnableDelete;
					tagControl.RemoveTagCommand = RemoveTagCommand;
				}
			}
		};

		return generator;
	}
}
