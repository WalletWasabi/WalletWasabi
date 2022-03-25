using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchItemViewModel
{
	private readonly SearchItem _item;

	public SearchItemViewModel(SearchItem item, Action collapseParent)
	{
		_item = item;
		Command = ReactiveCommand.CreateFromTask(async () =>
		{
			collapseParent();
			await item.OnExecution();
		});
	}

	public ReactiveCommand<Unit, Unit> Command { get; set; }

	public ComposedKey Key => _item.Key;
	public string Name => _item.Name;
	public IEnumerable<string> Keywords => _item.Keywords;
	public string Category => _item.Category;
	public string Description => _item.Description;
	public string? Icon => _item.Icon;
}