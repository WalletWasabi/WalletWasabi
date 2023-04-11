using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public class TagsBoxTopItemsPresenter : TemplatedControl
{
	private ICommand? _addTagCommand;
	private IEnumerable<string>? _topItems;

	public static readonly DirectProperty<TagsBoxTopItemsPresenter, IEnumerable<string>?> TopItemsProperty =
		AvaloniaProperty.RegisterDirect<TagsBoxTopItemsPresenter, IEnumerable<string>?>(
			nameof(TopItems),
			o => o.TopItems,
			(o, v) => o.TopItems = v);

	public static readonly DirectProperty<TagsBoxTopItemsPresenter, ICommand?> AddTagCommandProperty =
		AvaloniaProperty.RegisterDirect<TagsBoxTopItemsPresenter, ICommand?>(
			nameof(AddTagCommand),
			o => o.AddTagCommand,
			(o, v) => o.AddTagCommand = v);

	public IEnumerable<string>? TopItems
	{
		get => _topItems;
		set => SetAndRaise(TopItemsProperty, ref _topItems, value);
	}

	public ICommand? AddTagCommand
	{
		get => _addTagCommand;
		set => SetAndRaise(AddTagCommandProperty, ref _addTagCommand, value);
	}
}
